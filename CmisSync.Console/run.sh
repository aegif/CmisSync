#!/usr/bin/env bash

if [[ $UID -eq 0 ]]; then
  echo "CmisSync can't be run as root. Things would go utterly wrong."
  exit 1
fi

if [ "$XDG_RUNTIME_DIR" ]; then
  pidfile=${XDG_RUNTIME_DIR}/cmissync.pid
else
  pidfile=/tmp/cmissync-${USER}.pid
fi

start() {
  if [ -e "${pidfile}" ]; then
    cmissyncpid=`cat ${pidfile}`
    if [ -n "`ps -p ${cmissyncpid} | grep ${cmissyncpid}`" ]; then
      echo "CmisSync is already running."
      exit 0
    else
      echo "Stale CmisSync PID file found, starting a new instance..."
      rm -f $pidfile
    fi
  fi

  echo -n "Starting CmisSync... "
  if [ -n "${SSH_AGENT_PID}" -o -n "${SSH_AUTH_SOCK}" ] ; then
    @SLIMMONO@ @expanded_libdir@/@PACKAGE@/CmisSync.Console@MONOEXE@ $2 &
  else
    ssh-agent @SLIMMONO@ @expanded_libdir@/@PACKAGE@/CmisSync.Console@MONOEXE@ $2 &
  fi
  ( umask 066; echo $! > ${pidfile} )
  echo "Done."
}

stop() {
  if [ -e "${pidfile}" ]; then
    cmissyncpid=`cat ${pidfile}`
    if [ -n "`ps -p ${cmissyncpid} | grep ${cmissyncpid}`" ]; then
      echo -n "Stopping CmisSync... "
      kill ${cmissyncpid}
      rm -f ${pidfile}
      echo "Done."
    else
      echo "CmisSync is not running, removing stale PID file..."
      rm -f ${pidfile}
    fi
  else
    echo "CmisSync is not running."
  fi
}

case $1 in
  start|--start)
    start
    ;;
  stop|--stop)
    stop
    ;;
  restart|--restart)
    stop
    start
    ;;
  *)
    @SLIMMONO@ @expanded_libdir@/@PACKAGE@/CmisSync.Console@MONOEXE@ --help
    ;;
esac
