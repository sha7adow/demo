# ============ Build stage ============
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 复制工程文件先做 restore，利于 docker 层缓存
COPY ["进销存demo.csproj", "./"]
RUN dotnet restore "进销存demo.csproj"

# 再拷全部源码并发布
COPY . .
RUN dotnet publish "进销存demo.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ============ Runtime stage ============
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    TZ=Asia/Shanghai

# 中文字体 & 时区（SQLite 需要基础 glibc 支持即可，基础镜像已自带）
RUN apt-get update \
 && apt-get install -y --no-install-recommends tzdata \
 && rm -rf /var/lib/apt/lists/* \
 && ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

COPY --from=build /app/publish .

# 数据目录（sqlite 文件 & 日志）挂载点
RUN mkdir -p /app/data /app/Logs
VOLUME ["/app/data", "/app/Logs"]

EXPOSE 8080
ENTRYPOINT ["dotnet", "进销存demo.dll"]
