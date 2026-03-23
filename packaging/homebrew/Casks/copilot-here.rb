# typed: false
# frozen_string_literal: true

cask "copilot-here" do
  version "VERSION_PLACEHOLDER"

  on_arm do
    url "https://github.com/GordonBeeming/copilot_here/releases/download/TAG_PLACEHOLDER/copilot_here-osx-arm64.tar.gz"
    sha256 "SHA256_OSX_ARM64_PLACEHOLDER"
  end

  on_intel do
    url "https://github.com/GordonBeeming/copilot_here/releases/download/TAG_PLACEHOLDER/copilot_here-osx-x64.tar.gz"
    sha256 "SHA256_OSX_X64_PLACEHOLDER"
  end

  name "copilot_here"
  desc "Run GitHub Copilot CLI in a sandboxed Docker container"
  homepage "https://github.com/GordonBeeming/copilot_here"

  binary "copilot_here"

  caveats <<~EOS
    copilot_here requires Docker, Podman, or OrbStack to be installed and running.

    To enable the shell function wrapper, run:
      copilot_here --install-shells

    Or manually source the shell script in your profile:
      Bash/Zsh: source "$(brew --prefix)/share/copilot_here/copilot_here.sh"
  EOS
end
