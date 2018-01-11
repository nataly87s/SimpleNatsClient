set -e

docker-compose up -d

dotnet restore
dotnet test
