brew create https://github.com/CirroBuild/FoG/releases/download/v$1.preview/Frosti-v$1.preview-mac-arm64.tar.gz

brew create https://github.com/CirroBuild/FoG/releases/download/v$1.preview/Frosti-v$1.preview-mac-x64.tar.gz

brew create https://github.com/CirroBuild/FoG/releases/download/v$1.preview/Frosti-v$1.preview-win64.zip

brew create https://github.com/CirroBuild/FoG/releases/download/v$1.preview/Frosti-v$1.preview-win32.zip

rm /opt/homebrew/Library/Taps/homebrew/homebrew-core/Formula/f.rb
rm /opt/homebrew/Library/Taps/homebrew/homebrew-core/Formula/fr.rb
rm /opt/homebrew/Library/Taps/homebrew/homebrew-core/Formula/fro.rb
rm /opt/homebrew/Library/Taps/homebrew/homebrew-core/Formula/fros.rb
rm /opt/homebrew/Library/Taps/homebrew/homebrew-core/Formula/frost.rb
rm /opt/homebrew/Library/Taps/homebrew/homebrew-core/Formula/frosti.rb
