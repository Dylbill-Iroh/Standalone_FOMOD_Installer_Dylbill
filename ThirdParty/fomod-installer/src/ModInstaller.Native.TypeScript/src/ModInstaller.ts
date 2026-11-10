import { addon } from './resolve-native';
import * as types from './types';

const native: types.IModInstallerExtension = addon;

export class NativeModInstaller implements types.ModInstaller {
  private manager: types.ModInstaller;

  public constructor(
    pluginsGetAll: (activeOnly: boolean) => string[],
    contextGetAppVersion: () => string,
    contextGetCurrentGameVersion: () => string,
    contextGetExtenderVersion: (extender: string) => string,
    uiStartDialog: (moduleName: string, image: types.IHeaderImage, selectCallback: types.SelectCallback, contCallback: types.ContinueCallback, cancelCallback: types.CancelCallback) => void,
    uiEndDialog: () => void,
    uiUpdateState: (installSteps: types.IInstallStep[], currentStep: number) => void
  ) {
    this.manager = new native.ModInstaller(
      pluginsGetAll,
      contextGetAppVersion,
      contextGetCurrentGameVersion,
      contextGetExtenderVersion,
      uiStartDialog,
      uiEndDialog,
      uiUpdateState
    );
  }

  public install(files: string[], stopPatterns: string[], pluginPath: string,
    scriptPath: string, preset: any, preselect: boolean, validate: boolean): Promise<types.InstallResult | null> {
    return this.manager.install(files, stopPatterns, pluginPath, scriptPath, preset, preselect, validate);
  }

  public static testSupported = (files: string[], allowedTypes: string[]): types.SupportedResult => {
    return native.ModInstaller.testSupported(files, allowedTypes);
  }
}