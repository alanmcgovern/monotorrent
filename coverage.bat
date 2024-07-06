dotnet test --maxcpucount:10 --collect:"XPlat Code Coverage" src\MonoTorrent.sln
"%UserProfile%\.nuget\packages\reportgenerator\5.3.7\tools\net8.0\ReportGenerator.exe" -reports:**\coverage.cobertura.xml -targetdir:coveragereport
coveragereport\index.html

