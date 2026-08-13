import * as signalR from '@microsoft/signalr';

let connection: signalR.HubConnection | null = null;

export function getSimulationHub(): signalR.HubConnection {
  if (connection) return connection;

  connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/simulation')
    .withAutomaticReconnect()
    .build();

  return connection;
}