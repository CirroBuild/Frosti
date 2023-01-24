dotnet publish -c Release /p:PublishSingleFile=true -r osx-x64 --self-contained -o intel
dotnet publish -c Release /p:PublishSingleFile=true -r osx-arm64 --self-contained -o arm64
lipo -create -output arm64/Frosti arm64/Frosti
lipo -create -output intel/Frosti intel/Frosti

tar -zvcf Frosti-v0.8.preview-arm64.tar.gz arm64/Frosti 
tar -zvcf Frosti-v0.8.preview-x64.tar.gz intel/Frosti 

