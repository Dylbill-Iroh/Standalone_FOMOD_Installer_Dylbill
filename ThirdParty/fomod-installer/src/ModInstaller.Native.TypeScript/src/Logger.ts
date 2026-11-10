import { addon } from './resolve-native';
import * as types from './types';

const native: types.ILoggerExtension = addon;

export class NativeLogger implements types.Logger {
  private manager: types.Logger;

  public constructor(
    log: (level: number, message: string) => void
  ) {
    this.manager = new native.Logger(
      log
    );
  }

  public setCallbacks(): void {
    return this.manager.setCallbacks();
  }

  public disposeDefaultLogger(): void {
    return this.manager.disposeDefaultLogger();
  }

  public static setDefaultCallbacks = (): void => {
    return native.Logger.setDefaultCallbacks();
  }
}