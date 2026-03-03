# typed: false
# frozen_string_literal: true

class CopilotHere < Formula
  desc "Run GitHub Copilot CLI in a sandboxed Docker container"
  homepage "https://github.com/GordonBeeming/copilot_here"
  version "VERSION_PLACEHOLDER"
  license "FSL-1.1-MIT"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/GordonBeeming/copilot_here/releases/download/TAG_PLACEHOLDER/copilot_here-osx-arm64.tar.gz"
      sha256 "SHA256_OSX_ARM64_PLACEHOLDER"
    else
      url "https://github.com/GordonBeeming/copilot_here/releases/download/TAG_PLACEHOLDER/copilot_here-osx-x64.tar.gz"
      sha256 "SHA256_OSX_X64_PLACEHOLDER"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "https://github.com/GordonBeeming/copilot_here/releases/download/TAG_PLACEHOLDER/copilot_here-linux-arm64.tar.gz"
      sha256 "SHA256_LINUX_ARM64_PLACEHOLDER"
    else
      url "https://github.com/GordonBeeming/copilot_here/releases/download/TAG_PLACEHOLDER/copilot_here-linux-x64.tar.gz"
      sha256 "SHA256_LINUX_X64_PLACEHOLDER"
    end
  end

  depends_on "docker" => :recommended

  def install
    bin.install "copilot_here"
  end

  def caveats
    <<~EOS
      To enable the shell function wrapper, run:
        copilot_here --install-shells

      Or manually source the shell script in your profile:
        Bash/Zsh: source "$(brew --prefix)/share/copilot_here/copilot_here.sh"
    EOS
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/copilot_here --version")
  end
end
