import { addon } from './resolve-native';
import * as types from './types';

const native: types.IFileSystemExtension = addon;

export class NativeFileSystem implements types.FileSystem {
  private manager: types.FileSystem;

  public constructor(
    readFileContent: (filePath: string, offset: number, length: number) => Uint8Array | null,
    readDirectoryFileList: (directoryPath: string, pattern: string, searchType: number) => string[] | null,
    readDirectoryList: (directoryPath: string) => string[] | null
  ) {
    this.manager = new native.FileSystem(
      readFileContent,
      readDirectoryFileList,
      readDirectoryList
    );
  }

  public setCallbacks(): void {
    return this.manager.setCallbacks();
  }

  public static setDefaultCallbacks = (): void => {
    return native.FileSystem.setDefaultCallbacks();
  }
}