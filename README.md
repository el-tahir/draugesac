# Draugesac 

## publish the lambda function
```bash
cd lambda/Draugesac.Processor/src/Draugesac.Processor
```

```bash
dotnet publish -c Release -o ./bin/Release/net8.0/publish
```

## deploy aws cdk
```bash
cd infra
```

```bash
dotnet build src
```

```bash
cdk bootstrap
```

```bash
cdk deploy
```

## run the backend
```bash
cd src/Draugesac.Api
```

```bash
docker compose up
```

## run the frontend
```bash
cd src/Draugesac.UI
```

```bash
dotnet run
```
