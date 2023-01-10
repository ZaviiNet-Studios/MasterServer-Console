FROM artixlinux/base:latest
ADD . .
EXPOSE 7777/udp
CMD ["/game/UnityServer.x86_64", "-nographics", "-batchmode","-logfile output.log & touch output.log & tail -f output.log"]
