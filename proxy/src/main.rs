//! Secure Network Proxy
//!
//! HTTP CONNECT proxy that intercepts HTTPS traffic and enforces allow/block rules.
//! Designed to work with HTTP_PROXY/HTTPS_PROXY environment variables.

use anyhow::Result;
use rcgen::{BasicConstraints, CertificateParams, DistinguishedName, DnType, IsCa, KeyPair, Certificate};
use rustls::crypto::aws_lc_rs;
use rustls::pki_types::{CertificateDer, PrivateKeyDer, PrivatePkcs8KeyDer};
use rustls::ServerConfig;
use serde::Deserialize;
use std::{
    fs::{self, OpenOptions},
    io::Write,
    net::SocketAddr,
    path::Path,
    sync::Arc,
};
use tokio::io::{AsyncReadExt, AsyncWriteExt};
use tokio::net::{TcpListener, TcpStream};
use tokio_rustls::{TlsAcceptor, TlsConnector};
use tracing::{info, error, Level};
use tracing_subscriber::FmtSubscriber;

// ============================================================================
// Configuration
// ============================================================================

#[derive(Debug, Clone, Deserialize)]
struct HostRule {
    host: String,
    #[serde(default)]
    allowed_paths: Vec<String>,
    #[serde(default)]
    allow_insecure: bool,
}

#[derive(Debug, Clone, Deserialize)]
struct Config {
    #[serde(default = "default_mode")]
    mode: String,
    #[serde(default)]
    enable_logging: Option<bool>,
    #[serde(default)]
    allowed_rules: Vec<HostRule>,
}

fn default_mode() -> String {
    "monitor".to_string()
}

impl Config {
    /// Returns true if logging should be enabled
    /// Logging is enabled if: explicitly set to true, OR mode is "monitor"
    fn should_log(&self) -> bool {
        self.enable_logging.unwrap_or(self.mode == "monitor")
    }
}

impl Default for Config {
    fn default() -> Self {
        Self {
            mode: "monitor".to_string(),
            enable_logging: None,
            allowed_rules: vec![],
        }
    }
}

// ============================================================================
// Logging
// ============================================================================

fn log_traffic(config: &Config, action: &str, host: &str, path: &str, method: &str, mode: &str, reason: &str) {
    if !config.should_log() {
        return;
    }
    
    let log_path = "/logs/traffic.jsonl";
    if let Some(parent) = Path::new(log_path).parent() {
        let _ = fs::create_dir_all(parent);
    }
    if let Ok(mut file) = OpenOptions::new().create(true).append(true).open(log_path) {
        let entry = serde_json::json!({
            "action": action,
            "host": host,
            "path": path,
            "method": method,
            "mode": mode,
            "reason": reason
        });
        let _ = writeln!(file, "{}", entry);
    }
}

// ============================================================================
// Security Check
// ============================================================================

/// Check if a host is allowed and whether insecure is permitted (for CONNECT-level checks, ignores path rules)
fn check_host_allowed(config: &Config, host: &str) -> (bool, String, bool) {
    if config.mode != "enforce" {
        return (true, "Monitor Mode".to_string(), true);
    }

    let host_rule = config.allowed_rules.iter().find(|rule| {
        host == rule.host || host.ends_with(&format!(".{}", rule.host))
    });

    match host_rule {
        None => (false, "Host Not Allowed".to_string(), false),
        Some(rule) => (true, "Host Allowed".to_string(), rule.allow_insecure),
    }
}

/// Check if a request (host + path) is allowed
fn check_request(config: &Config, host: &str, path: &str) -> (bool, String) {
    if config.mode != "enforce" {
        return (true, "Monitor Mode".to_string());
    }

    let host_rule = config.allowed_rules.iter().find(|rule| {
        host == rule.host || host.ends_with(&format!(".{}", rule.host))
    });

    match host_rule {
        None => (false, "Host Not Allowed".to_string()),
        Some(rule) => {
            if rule.allowed_paths.is_empty() {
                // Empty allowed_paths means no paths are allowed - require explicit "*" for all paths
                return (false, "No Paths Configured".to_string());
            }
            let path_match = rule.allowed_paths.iter().any(|p| {
                if p == "*" {
                    // Explicit wildcard for all paths
                    true
                } else if let Some(prefix) = p.strip_suffix('*') {
                    path.starts_with(prefix)
                } else {
                    path == p
                }
            });
            if path_match {
                (true, "Path Match".to_string())
            } else {
                (false, "Path Not Allowed".to_string())
            }
        }
    }
}

// ============================================================================
// HTTP CONNECT Parsing
// ============================================================================

/// Request type parsed from the incoming connection
enum ProxyRequest {
    Connect { host: String, port: u16 },
    Http { method: String, url: String, headers: String },
    Health,
    Unknown,
}

/// Parse HTTP request - CONNECT for HTTPS proxy, regular HTTP methods, or GET /health for healthcheck
async fn read_request(client: &mut TcpStream) -> Result<ProxyRequest> {
    let mut buf = vec![0u8; 8192];
    let mut total_read = 0;
    
    // Read until we find \r\n\r\n (end of headers)
    loop {
        let n = client.read(&mut buf[total_read..]).await?;
        if n == 0 {
            return Ok(ProxyRequest::Unknown);
        }
        total_read += n;
        
        // Check for end of headers
        if let Some(_) = buf[..total_read].windows(4).position(|w| w == b"\r\n\r\n") {
            break;
        }
        
        if total_read >= buf.len() {
            return Ok(ProxyRequest::Unknown); // Headers too large
        }
    }
    
    let request = String::from_utf8_lossy(&buf[..total_read]);
    let first_line = request.lines().next().unwrap_or("");
    let parts: Vec<&str> = first_line.split_whitespace().collect();
    
    if parts.len() < 2 {
        return Ok(ProxyRequest::Unknown);
    }

    let method = parts[0];
    let target = parts[1];

    // Check for health endpoint (direct request, not proxied)
    if method == "GET" && target == "/health" {
        return Ok(ProxyRequest::Health);
    }
    
    // CONNECT method for HTTPS tunneling
    if method == "CONNECT" {
        // Parse host:port from CONNECT target
        let (host, port) = if let Some(colon_pos) = target.rfind(':') {
            let host = &target[..colon_pos];
            let port: u16 = target[colon_pos + 1..].parse().unwrap_or(443);
            (host.to_string(), port)
        } else {
            (target.to_string(), 443)
        };
        
        return Ok(ProxyRequest::Connect { host, port });
    }
    
    // HTTP proxy request (GET http://host/path, POST http://host/path, etc.)
    if target.starts_with("http://") {
        // Collect headers (skip first line)
        let headers: String = request.lines().skip(1).collect::<Vec<&str>>().join("\r\n");
        return Ok(ProxyRequest::Http { 
            method: method.to_string(), 
            url: target.to_string(),
            headers,
        });
    }
    
    Ok(ProxyRequest::Unknown)
}

// ============================================================================
// Certificate Authority
// ============================================================================

struct CaAuthority {
    ca_key: KeyPair,
    ca_cert: Certificate,
}

impl CaAuthority {
    fn new() -> Result<Self> {
        let ca_cert_path = "/ca/certs/ca.pem";
        let ca_key_path = "/ca/keys/ca.private.key";

        fs::create_dir_all("/ca/certs")?;
        fs::create_dir_all("/ca/keys")?;

        info!("Generating CA certificate...");

        let mut params = CertificateParams::default();
        params.is_ca = IsCa::Ca(BasicConstraints::Unconstrained);
        let mut dn = DistinguishedName::new();
        dn.push(DnType::CommonName, "Secure Proxy CA");
        dn.push(DnType::OrganizationName, "Secure Proxy");
        params.distinguished_name = dn;

        let key_pair = KeyPair::generate()?;
        let cert = params.self_signed(&key_pair)?;

        fs::write(ca_cert_path, cert.pem())?;
        fs::write(ca_key_path, key_pair.serialize_pem())?;

        info!("CA saved to {}", ca_cert_path);

        Ok(Self {
            ca_key: key_pair,
            ca_cert: cert,
        })
    }

    fn generate_cert_for_host(&self, hostname: &str) -> Result<(Vec<CertificateDer<'static>>, PrivateKeyDer<'static>)> {
        let mut params = CertificateParams::new(vec![hostname.to_string()])?;
        let mut dn = DistinguishedName::new();
        dn.push(DnType::CommonName, hostname);
        params.distinguished_name = dn;

        let key_pair = KeyPair::generate()?;
        let cert = params.signed_by(&key_pair, &self.ca_cert, &self.ca_key)?;

        let cert_der = CertificateDer::from(cert.der().to_vec());
        let key_der = PrivateKeyDer::Pkcs8(PrivatePkcs8KeyDer::from(key_pair.serialize_der()));

        Ok((vec![cert_der], key_der))
    }
}

// ============================================================================
// Connection Handler
// ============================================================================

/// Handle plain HTTP proxy requests (not CONNECT tunneling)
async fn handle_http_request(
    mut client: TcpStream,
    config: Arc<Config>,
    method: String,
    url: String,
    headers: String,
) -> Result<()> {
    // Parse URL: http://host:port/path
    let url_without_scheme = url.strip_prefix("http://").unwrap_or(&url);
    let (host_port, path) = match url_without_scheme.find('/') {
        Some(idx) => (&url_without_scheme[..idx], &url_without_scheme[idx..]),
        None => (url_without_scheme, "/"),
    };
    
    let (hostname, port) = if let Some(colon_pos) = host_port.rfind(':') {
        let host = &host_port[..colon_pos];
        let port: u16 = host_port[colon_pos + 1..].parse().unwrap_or(80);
        (host.to_string(), port)
    } else {
        (host_port.to_string(), 80)
    };

    // Check if host is allowed and if insecure is permitted
    let (host_allowed, reason, allow_insecure) = check_host_allowed(&config, &hostname);
    
    if !host_allowed {
        log_traffic(&config, "BLOCK", &hostname, path, &method, &config.mode, &reason);
        println!("⛔ [{}] {} http://{}{} -> {}", config.mode, method, hostname, path, reason);
        let response = "HTTP/1.1 403 Forbidden\r\nContent-Type: text/plain\r\n\r\nHost not allowed";
        client.write_all(response.as_bytes()).await?;
        return Ok(());
    }

    // Block insecure requests unless explicitly allowed
    if !allow_insecure && config.mode == "enforce" {
        log_traffic(&config, "BLOCK", &hostname, path, &method, &config.mode, "Insecure HTTP not allowed");
        println!("⛔ [{}] {} http://{}{} -> Insecure HTTP not allowed (set allow_insecure: true)", config.mode, method, hostname, path);
        let response = "HTTP/1.1 403 Forbidden\r\nContent-Type: text/plain\r\n\r\nInsecure HTTP not allowed. Use HTTPS or set allow_insecure: true for this host.";
        client.write_all(response.as_bytes()).await?;
        return Ok(());
    }

    // Check path-level rules
    let (allowed, reason) = check_request(&config, &hostname, path);
    
    if !allowed {
        log_traffic(&config, "BLOCK", &hostname, path, &method, &config.mode, &reason);
        println!("⛔ [{}] {} http://{}{} -> {}", config.mode, method, hostname, path, reason);
        let response = "HTTP/1.1 403 Forbidden\r\nContent-Type: text/plain\r\n\r\nPath not allowed";
        client.write_all(response.as_bytes()).await?;
        return Ok(());
    }

    let action = "ALLOW";
    log_traffic(&config, action, &hostname, path, &method, &config.mode, &reason);
    println!("✅ [{}] {} http://{}{} -> {}", config.mode, method, hostname, path, reason);

    // Connect to upstream
    let upstream_addr = format!("{}:{}", hostname, port);
    let mut upstream = match TcpStream::connect(&upstream_addr).await {
        Ok(s) => s,
        Err(e) => {
            error!("Failed to connect to upstream {}: {}", upstream_addr, e);
            let response = format!("HTTP/1.1 502 Bad Gateway\r\n\r\nFailed to connect to {}", hostname);
            client.write_all(response.as_bytes()).await?;
            return Ok(());
        }
    };

    // Reconstruct and forward the HTTP request (convert absolute URL to relative path)
    let request = format!("{} {} HTTP/1.1\r\nHost: {}\r\n{}\r\n\r\n", 
        method, path, hostname, headers);
    upstream.write_all(request.as_bytes()).await?;

    // Bidirectional copy
    let (mut client_read, mut client_write) = client.into_split();
    let (mut upstream_read, mut upstream_write) = upstream.into_split();

    let client_to_upstream = tokio::io::copy(&mut client_read, &mut upstream_write);
    let upstream_to_client = tokio::io::copy(&mut upstream_read, &mut client_write);

    tokio::select! {
        _ = client_to_upstream => {},
        _ = upstream_to_client => {},
    }

    Ok(())
}

async fn handle_connection(
    mut client: TcpStream,
    ca: Arc<CaAuthority>,
    config: Arc<Config>,
) -> Result<()> {
    // Parse HTTP request (CONNECT, HTTP, or health check)
    let request = read_request(&mut client).await?;
    
    let (hostname, port) = match request {
        ProxyRequest::Health => {
            // Respond to health check
            let response = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 2\r\n\r\nOK";
            client.write_all(response.as_bytes()).await?;
            return Ok(());
        }
        ProxyRequest::Http { method, url, headers } => {
            // Handle plain HTTP proxy request
            return handle_http_request(client, config, method, url, headers).await;
        }
        ProxyRequest::Connect { host, port } => (host, port),
        ProxyRequest::Unknown => {
            error!("Failed to parse request");
            let response = "HTTP/1.1 400 Bad Request\r\n\r\n";
            client.write_all(response.as_bytes()).await?;
            return Ok(());
        }
    };

    // Check if host is allowed (for CONNECT-level blocking)
    let (host_allowed, reason, _allow_insecure) = check_host_allowed(&config, &hostname);
    
    if !host_allowed {
        log_traffic(&config, "BLOCK", &hostname, "/", "CONNECT", &config.mode, &reason);
        println!("⛔ [{}] CONNECT {}:{} -> {}", config.mode, hostname, port, reason);
        let response = "HTTP/1.1 403 Forbidden\r\nContent-Type: text/plain\r\n\r\nHost not allowed";
        client.write_all(response.as_bytes()).await?;
        return Ok(());
    }

    // Connect to upstream first to verify it's reachable
    let upstream_addr = format!("{}:{}", hostname, port);
    let upstream = match TcpStream::connect(&upstream_addr).await {
        Ok(s) => s,
        Err(e) => {
            error!("Failed to connect to upstream {}: {}", upstream_addr, e);
            let response = format!("HTTP/1.1 502 Bad Gateway\r\n\r\nFailed to connect to {}", hostname);
            client.write_all(response.as_bytes()).await?;
            return Ok(());
        }
    };

    // Send 200 Connection Established to client
    client.write_all(b"HTTP/1.1 200 Connection Established\r\n\r\n").await?;

    // Generate certificate for this host
    let (certs, key) = ca.generate_cert_for_host(&hostname)?;

    // Create TLS config for client-facing connection
    let server_config = ServerConfig::builder()
        .with_no_client_auth()
        .with_single_cert(certs, key)?;
    
    let acceptor = TlsAcceptor::from(Arc::new(server_config));

    // Accept TLS from client
    let mut client_tls = acceptor.accept(client).await?;

    // Create TLS connection to upstream
    let connector = TlsConnector::from(Arc::new(
        rustls::ClientConfig::builder()
            .with_root_certificates(rustls::RootCertStore::from_iter(
                webpki_roots::TLS_SERVER_ROOTS.iter().cloned()
            ))
            .with_no_client_auth()
    ));

    let server_name = hostname.clone().try_into()?;
    let mut upstream_tls = connector.connect(server_name, upstream).await?;

    // Now we have decrypted streams. Read HTTP request.
    let mut request_buf = vec![0u8; 8192];
    let n = client_tls.read(&mut request_buf).await?;
    let request_data = &request_buf[..n];

    // Parse HTTP request line
    let request_str = String::from_utf8_lossy(request_data);
    let first_line = request_str.lines().next().unwrap_or("");
    let parts: Vec<&str> = first_line.split_whitespace().collect();
    let (method, path) = if parts.len() >= 2 {
        (parts[0], parts[1])
    } else {
        ("?", "/")
    };

    // Check path-level rules
    let (allowed, reason) = check_request(&config, &hostname, path);
    let action = if allowed { "ALLOW" } else { "BLOCK" };
    log_traffic(&config, action, &hostname, path, method, &config.mode, &reason);

    let icon = if allowed { "✅" } else { "⛔" };
    println!("{} [{}] {} {}{} -> {}", icon, config.mode, method, hostname, path, reason);

    if !allowed {
        // Send 403 response
        let response = "HTTP/1.1 403 Forbidden\r\n\
             Content-Type: text/plain\r\n\
             Content-Length: 24\r\n\
             Connection: close\r\n\r\n\
             Blocked by Secure Proxy";
        client_tls.write_all(response.as_bytes()).await?;
        return Ok(());
    }

    // Forward request to upstream
    upstream_tls.write_all(request_data).await?;

    // Bidirectional copy
    let (mut client_read, mut client_write) = tokio::io::split(client_tls);
    let (mut upstream_read, mut upstream_write) = tokio::io::split(upstream_tls);

    let client_to_upstream = tokio::io::copy(&mut client_read, &mut upstream_write);
    let upstream_to_client = tokio::io::copy(&mut upstream_read, &mut client_write);

    tokio::select! {
        _ = client_to_upstream => {},
        _ = upstream_to_client => {},
    }

    Ok(())
}

// ============================================================================
// Main
// ============================================================================

#[tokio::main]
async fn main() -> Result<()> {
    let subscriber = FmtSubscriber::builder()
        .with_max_level(Level::INFO)
        .with_target(false)
        .finish();
    tracing::subscriber::set_global_default(subscriber)?;

    println!("🔧 Initializing Secure Proxy (Airlock Mode)...");

    // Install the crypto provider globally
    aws_lc_rs::default_provider()
        .install_default()
        .expect("Failed to install crypto provider");

    // Load config
    let config_path = "/config/rules.json";
    let config: Config = if Path::new(config_path).exists() {
        let content = fs::read_to_string(config_path)?;
        serde_json::from_str(&content)?
    } else {
        println!("[Config] No config found, using MONITOR mode");
        Config::default()
    };
    println!("[Config] Loaded mode: {}", config.mode.to_uppercase());
    let config = Arc::new(config);

    // Setup CA
    let ca = Arc::new(CaAuthority::new()?);
    println!("🔒 CA Certificate ready");

    // Create listener
    let addr = SocketAddr::from(([0, 0, 0, 0], 58080));
    let listener = TcpListener::bind(addr).await?;

    println!("🛡️  Secure Proxy listening on 0.0.0.0:58080");
    println!("✅ Environment Ready.");

    loop {
        let (client, peer_addr) = listener.accept().await?;
        let ca = ca.clone();
        let config = config.clone();

        tokio::spawn(async move {
            if let Err(e) = handle_connection(client, ca, config).await {
                error!("Connection error from {}: {}", peer_addr, e);
            }
        });
    }
}
