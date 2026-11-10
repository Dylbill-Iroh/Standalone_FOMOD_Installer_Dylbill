import { addon } from './resolve-native';
import * as types from './types';

const native: types.IExtension = addon;

export const allocWithOwnership = (length: number): Uint8Array | null => {
  return native.allocWithOwnership(length);
}
export const allocWithoutOwnership = (length: number): Uint8Array | null => {
  return native.allocWithoutOwnership(length);
}
export const allocAliveCount = (): number => {
  return native.allocAliveCount();
}