rm Frosti-v*

dotnet publish -c Release /p:PublishSingleFile=true -r osx-arm64 --self-contained -o mac_arm64
dotnet publish -c Release /p:PublishSingleFile=true -r osx-x64 --self-contained -o mac_x64
dotnet publish -c Release /p:PublishSingleFile=true -r win-x64 --self-contained -o win64
dotnet publish -c Release /p:PublishSingleFile=true -r win-x86 --self-contained -o win32

lipo -create -output mac_arm64/Frosti mac_arm64/Frosti
lipo -create -output mac_x64/Frosti mac_x64/Frosti

tar -zvcf Frosti-v$1.preview-mac-arm64.tar.gz mac_arm64/Frosti 
tar -zvcf Frosti-v$1.preview-mac-x64.tar.gz mac_x64/Frosti

zip Frosti-v$1.preview-win64.zip win64/Frosti.exe
zip Frosti-v$1.preview-win32.zip win32/Frosti.exe
