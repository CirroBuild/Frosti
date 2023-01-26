dotnet publish -c Release /p:PublishSingleFile=true -r osx-x64 --self-contained -o intel
dotnet publish -c Release /p:PublishSingleFile=true -r osx-arm64 --self-contained -o arm64
dotnet publish -c Release /p:PublishSingleFile=true -r win-x64 --self-contained -o win64
dotnet publish -c Release /p:PublishSingleFile=true -r win-x86 --self-contained -o win32

lipo -create -output arm64/Frosti arm64/Frosti
lipo -create -output intel/Frosti intel/Frosti

tar -zvcf Frosti-v$1.preview-arm64.tar.gz arm64/Frosti 
tar -zvcf Frosti-v$1.preview-x64.tar.gz intel/Frosti 

zip Frosti-v$1.preview-win32.zip win32/Frosti.exe
zip Frosti-v$1.preview-win64.zip win64/Frosti.exe
