import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { buildApiUrl } from "@/services/http";

export function createInterviewHub(token: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(buildApiUrl("/hubs/interview"), {
      accessTokenFactory: () => token,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}

export async function stopInterviewHub(connection: HubConnection | null) {
  if (!connection) {
    return;
  }

  if (connection.state !== HubConnectionState.Disconnected) {
    await connection.stop();
  }
}
