dotnet publish -c Release /p:PublishSingleFile=true -r osx-x64 --self-contained -o intel
dotnet publish -c Release /p:PublishSingleFile=true -r osx-arm64 --self-contained -o arm64
lipo -create -output arm64/Frosti arm64/Frosti
lipo -create -output intel/Frosti intel/Frosti

tar -zvcf Frosti-v0.4.preview-arm64.tar.gz arm64/Frosti 
tar -zvcf Frosti-v0.4preview-x64.tar.gz intel/Frosti 

