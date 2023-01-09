FROM ubuntu:18.04
ADD . .
RUN apt-get update && apt-get install -qqq libicu60 -y
EXPOSE 8080 13000
CMD ["/Server/MasterServer"]