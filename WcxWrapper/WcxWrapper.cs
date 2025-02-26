﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using TcPluginBase;
using TcPluginBase.Packer;
using TcPluginBase.Tools;


namespace WcxWrapper {
    public class PackerWrapper {
        private static string? _callSignature;
        private static PackerPlugin? _plugin;
        private static PackerPlugin Plugin => _plugin ??= TcPluginLoader.GetTcPlugin<PackerPlugin>(typeof(PluginClassPlaceholder));


        private PackerWrapper()
        {
        }

        #region Packer Plugin Exported Functions

        #region Mandatory Methods

        #region OpenArchive

        [UnmanagedCallersOnly(EntryPoint = "OpenArchive")]
        public static IntPtr OpenArchive(IntPtr archiveData)
        {
            var data = new OpenArchiveData(archiveData, false);
            return OpenArchiveInternal(data);
        }

        [UnmanagedCallersOnly(EntryPoint = "OpenArchiveW")]
        public static IntPtr OpenArchiveW(IntPtr archiveData)
        {
            var data = new OpenArchiveData(archiveData, true);
            return OpenArchiveInternal(data);
        }

        public static IntPtr OpenArchiveInternal(OpenArchiveData data)
        {
            var result = IntPtr.Zero;
            _callSignature = $"OpenArchive {data.ArchiveName} ({data.Mode})";
            try {
                var o = Plugin.OpenArchive(ref data);
                if (o != null && data.Result == PackerResult.OK) {
                    result = TcHandles.AddHandle(o);
                    data.Update();
                }

                TraceCall(TraceLevel.Info, result == IntPtr.Zero ? $"Error ({data.Result})" : result.ToString());
            }
            catch (Exception ex) {
                ProcessException(ex);
                result = IntPtr.Zero;
            }

            return result;
        }

        #endregion OpenArchive

        #region ReadHeader

        [UnmanagedCallersOnly(EntryPoint = "ReadHeader")]
        public static int ReadHeader(IntPtr arcData, IntPtr headerData)
        {
            return ReadHeaderInternal(arcData, headerData, HeaderDataMode.Ansi);
        }

        #endregion ReadHeader

        #region ReadHeaderEx

        [UnmanagedCallersOnly(EntryPoint = "ReadHeaderEx")]
        public static int ReadHeaderEx(IntPtr arcData, IntPtr headerData)
        {
            return ReadHeaderInternal(arcData, headerData, HeaderDataMode.ExAnsi);
        }

        [UnmanagedCallersOnly(EntryPoint = "ReadHeaderExW")]
        public static int ReadHeaderExW(IntPtr arcData, IntPtr headerData)
        {
            return ReadHeaderInternal(arcData, headerData, HeaderDataMode.ExUnicode);
        }

        public static int ReadHeaderInternal(IntPtr arcData, IntPtr headerData, HeaderDataMode mode)
        {
            var result = PackerResult.EndArchive;
            _callSignature = $"ReadHeader ({arcData.ToString()})";
            try {
                var o = TcHandles.GetObject(arcData);
                if (o == null) {
                    return (int) PackerResult.ErrorOpen;
                }

                result = Plugin.ReadHeader(ref o, out var header);
                if (result == PackerResult.OK) {
                    header.CopyTo(headerData, mode);
                    TcHandles.UpdateHandle(arcData, o);
                }

                // !!! may produce much trace info !!!
                TraceCall(TraceLevel.Verbose, $"{result.ToString()} ({((result == PackerResult.OK) ? header.FileName : null)})");
            }
            catch (Exception ex) {
                ProcessException(ex);
            }

            return (int) result;
        }

        #endregion ReadHeaderEx

        #region ProcessFile

        [UnmanagedCallersOnly(EntryPoint = "ProcessFile")]
        public static int ProcessFile(IntPtr arcData, int operation, IntPtr destPathPtr, IntPtr destNamePtr)
        {
            var destPath = Marshal.PtrToStringAnsi(destPathPtr)!;
            var destName = Marshal.PtrToStringAnsi(destNamePtr)!;

            return ProcessFileInternal(arcData, operation, destPath, destName);
        }

        [UnmanagedCallersOnly(EntryPoint = "ProcessFileW")]
        public static int ProcessFileW(IntPtr arcData, int operation, IntPtr destPathPtr, IntPtr destNamePtr)
        {
            var destPath = Marshal.PtrToStringUni(destPathPtr)!;
            var destName = Marshal.PtrToStringUni(destNamePtr)!;

            return ProcessFileInternal(arcData, operation, destPath, destName);
        }

        private static int ProcessFileInternal(IntPtr arcData, int operation, string destPath, string destName)
        {
            var result = PackerResult.NotSupported;
            var oper = (ProcessFileOperation) operation;
            var fileName = string.IsNullOrEmpty(destPath) ? destName : Path.Combine(destPath, destName);
            _callSignature = $"ProcessFile ({arcData.ToString()}, {oper.ToString()}, {fileName})";
            try {
                var o = TcHandles.GetObject(arcData);
                if (o != null) {
                    result = Plugin.ProcessFile(o, oper, fileName);
                    if (result == PackerResult.OK) {
                        TcHandles.UpdateHandle(arcData, o);
                    }
                }

                // !!! may produce much trace info !!!
                TraceCall(TraceLevel.Verbose, result.ToString());
            }
            catch (Exception ex) {
                ProcessException(ex);
            }

            return (int) result;
        }

        #endregion ProcessFile

        #region CloseArchive

        [UnmanagedCallersOnly(EntryPoint = "CloseArchive")]
        public static int CloseArchive(IntPtr arcData)
        {
            var result = PackerResult.ErrorClose;
            _callSignature = $"FindClose ({arcData.ToString()})";
            try {
                var o = TcHandles.GetObject(arcData);
                if (o != null) {
                    result = Plugin.CloseArchive(o);
                    if (o is IDisposable dip) {
                        dip.Dispose();
                    }

                    var count = (TcHandles.RemoveHandle(arcData) - 1) / 2;

                    TraceCall(TraceLevel.Info, $"{count} items.");
                }
            }
            catch (Exception ex) {
                ProcessException(ex);
            }

            return (int) result;
        }

        #endregion CloseArchive

        #region SetChangeVolProc

        // SetChangeVolProc & SetChangeVolProcW functionality is implemented here, not included to Packer Plugin interface.
        [UnmanagedCallersOnly(EntryPoint = "SetChangeVolProc")]
        public static void SetChangeVolProc(IntPtr arcData, IntPtr changeVolProcPtr)
        {
            var changeVolProc = Marshal.GetDelegateForFunctionPointer<ChangeVolCallback>(changeVolProcPtr);

            _callSignature = $"SetChangeVolProc ({arcData.ToString()})";
            try {
                TcCallback.SetPackerPluginCallbacks(changeVolProc, null, null, null, null, null);

                TraceCall(TraceLevel.Warning, changeVolProc.Method.MethodHandle.GetFunctionPointer().ToString("X"));
            }
            catch (Exception ex) {
                ProcessException(ex);
            }
        }

        [UnmanagedCallersOnly(EntryPoint = "SetChangeVolProcW")]
        public static void SetChangeVolProcW(IntPtr arcData, IntPtr changeVolProcWPtr)
        {
            var changeVolProcW = Marshal.GetDelegateForFunctionPointer<ChangeVolCallbackW>(changeVolProcWPtr);

            _callSignature = $"SetChangeVolProcW ({arcData.ToString()})";
            try {
                TcCallback.SetPackerPluginCallbacks(null, changeVolProcW, null, null, null, null);

                TraceCall(TraceLevel.Warning, changeVolProcW.Method.MethodHandle.GetFunctionPointer().ToString());
            }
            catch (Exception ex) {
                ProcessException(ex);
            }
        }

        #endregion SetChangeVolProc

        #region SetProcessDataProc

        // SetProcessDataProc & SetProcessDataProcW functionality is implemented here, not included to Packer Plugin interface.
        [UnmanagedCallersOnly(EntryPoint = "SetProcessDataProc")]
        public static void SetProcessDataProc(IntPtr arcData, IntPtr processDataProcPtr)
        {
            var processDataProc = Marshal.GetDelegateForFunctionPointer<ProcessDataCallback>(processDataProcPtr);

            _callSignature = $"SetProcessDataProc ({arcData.ToString()})";
            try {
                TcCallback.SetPackerPluginCallbacks(null, null, processDataProc, null, null, null);

                TraceCall(TraceLevel.Warning, processDataProc.Method.MethodHandle.GetFunctionPointer().ToString());
            }
            catch (Exception ex) {
                ProcessException(ex);
            }
        }

        [UnmanagedCallersOnly(EntryPoint = "SetProcessDataProcW")]
        public static void SetProcessDataProcW(IntPtr arcData, IntPtr processDataProcWPtr)
        {
            var processDataProcW = Marshal.GetDelegateForFunctionPointer<ProcessDataCallbackW>(processDataProcWPtr);

            _callSignature = $"SetProcessDataProcW ({arcData.ToString()})";
            try {
                TcCallback.SetPackerPluginCallbacks(null, null, null, processDataProcW, null, null);

                TraceCall(TraceLevel.Warning, processDataProcW.Method.MethodHandle.GetFunctionPointer().ToString());
            }
            catch (Exception ex) {
                ProcessException(ex);
            }
        }

        #endregion SetProcessDataProc

        #endregion Mandatory Methods

        #region Optional Methods

        #region PackFiles

        [UnmanagedCallersOnly(EntryPoint = "PackFiles")]
        public static int PackFiles(
            IntPtr packedFilePtr,
            IntPtr subPathPtr,
            IntPtr srcPathPtr,
            IntPtr addListPtr, int flags)
        {
            var packedFile = Marshal.PtrToStringAnsi(packedFilePtr)!;
            var subPath = Marshal.PtrToStringAnsi(subPathPtr)!;
            var srcPath = Marshal.PtrToStringAnsi(srcPathPtr)!;

            List<string> addList = TcUtils.ReadStringListAnsi(addListPtr);
            return PackFilesInternal(packedFile, subPath, srcPath, addList, (PackFilesFlags) flags);
        }

        [UnmanagedCallersOnly(EntryPoint = "PackFilesW")]
        public static int PackFilesW(
            IntPtr packedFilePtr,
            IntPtr subPathPtr,
            IntPtr srcPathPtr,
            IntPtr addListPtr, int flags)
        {
            var packedFile = Marshal.PtrToStringUni(packedFilePtr)!;
            var subPath = Marshal.PtrToStringUni(subPathPtr)!;
            var srcPath = Marshal.PtrToStringUni(srcPathPtr)!;

            List<string> addList = TcUtils.ReadStringListUni(addListPtr);
            return PackFilesInternal(packedFile, subPath, srcPath, addList, (PackFilesFlags) flags);
        }

        public static int PackFilesInternal(string packedFile, string subPath, string srcPath, List<string> addList, PackFilesFlags flags)
        {
            var result = PackerResult.NotSupported;
            _callSignature = $"PackFiles ({packedFile}, {subPath}, {srcPath}, {flags.ToString()}) - {addList.Count} files)";
            try {
                result = Plugin.PackFiles(packedFile, subPath, srcPath, addList, flags);

                TraceCall(TraceLevel.Info, result.ToString());
            }
            catch (Exception ex) {
                ProcessException(ex);
            }

            return (int) result;
        }

        #endregion PackFiles

        #region DeleteFiles

        [UnmanagedCallersOnly(EntryPoint = "DeleteFiles")]
        public static int DeleteFiles(IntPtr packedFilePtr, IntPtr deleteListPtr)
        {
            var packedFile = Marshal.PtrToStringAnsi(packedFilePtr)!;

            var deleteList = TcUtils.ReadStringListAnsi(deleteListPtr);
            return DeleteFilesInternal(packedFile, deleteList);
        }

        [UnmanagedCallersOnly(EntryPoint = "DeleteFilesW")]
        public static int DeleteFilesW(IntPtr packedFilePtr, IntPtr deleteListPtr)
        {
            var packedFile = Marshal.PtrToStringUni(packedFilePtr)!;

            var deleteList = TcUtils.ReadStringListUni(deleteListPtr);
            return DeleteFilesInternal(packedFile, deleteList);
        }

        public static int DeleteFilesInternal(string packedFile, List<string> deleteList)
        {
            var result = PackerResult.NotSupported;
            _callSignature = $"DeleteFiles ({packedFile}) - {deleteList.Count} files)";
            try {
                result = Plugin.DeleteFiles(packedFile, deleteList);

                TraceCall(TraceLevel.Info, result.ToString());
            }
            catch (Exception ex) {
                ProcessException(ex);
            }

            return (int) result;
        }

        #endregion DeleteFiles

        #region GetPackerCaps

        // GetPackerCaps functionality is implemented here, not included to Packer Plugin interface.
        [UnmanagedCallersOnly(EntryPoint = "GetPackerCaps")]
        public static int GetPackerCaps()
        {
            _callSignature = "GetPackerCaps";

            TraceCall(TraceLevel.Info, Plugin.Capabilities.ToString());
            return (int) Plugin.Capabilities;
        }

        #endregion GetPackerCaps

        #region ConfigurePacker

        [UnmanagedCallersOnly(EntryPoint = "ConfigurePacker")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static void ConfigurePacker(IntPtr parentWin, IntPtr dllInstance)
        {
            _callSignature = "ConfigurePacker";
            try {
                Plugin.ConfigurePacker(new TcWindow(parentWin));

                TraceCall(TraceLevel.Info, null);
            }
            catch (Exception ex) {
                ProcessException(ex);
            }
        }

        #endregion ConfigurePacker

        #region StartMemPack

        [UnmanagedCallersOnly(EntryPoint = "StartMemPack")]
        public static IntPtr StartMemPack(int options, IntPtr fileNamePtr)
        {
            var fileName = Marshal.PtrToStringAnsi(fileNamePtr)!;
            return StartMemPackInternal(options, fileName);
        }

        [UnmanagedCallersOnly(EntryPoint = "StartMemPackW")]
        public static IntPtr StartMemPackW(int options, IntPtr fileNamePtr)
        {
            var fileName = Marshal.PtrToStringUni(fileNamePtr)!;
            return StartMemPackInternal(options, fileName);
        }

        private static IntPtr StartMemPackInternal(int options, string fileName)
        {
            var result = IntPtr.Zero;
            var mpOptions = (MemPackOptions) options;
            _callSignature = $"StartMemPack {fileName} ({mpOptions.ToString()})";
            try {
                var o = Plugin.StartMemPack(mpOptions, fileName);
                if (o != null) {
                    result = TcHandles.AddHandle(o);
                }

                TraceCall(TraceLevel.Warning, (result == IntPtr.Zero) ? "ERROR" : result.ToString());
            }
            catch (Exception ex) {
                ProcessException(ex);
            }

            return result;
        }

        #endregion StartMemPack

        #region PackToMem

        [UnmanagedCallersOnly(EntryPoint = "PackToMem")]
        public static int PackToMem(IntPtr hMemPack,
            IntPtr bufInPtr, int inLen, ref int taken,
            IntPtr bufOutPtr, int outLen, ref int written, int seekBy)
        {
            var bufIn = TcUtils.ReadByteArray(bufInPtr, inLen);
            var bufOut = TcUtils.ReadByteArray(bufOutPtr, outLen);

            var result = PackerResult.NotSupported;
            _callSignature = $"PackToMem ({hMemPack.ToString()} - {inLen}, {outLen}, {seekBy})";
            string? traceRes = null;
            try {
                var o = TcHandles.GetObject(hMemPack);
                if (o != null) {
                    result = Plugin.PackToMem(ref o, bufIn, ref taken, bufOut, ref written, seekBy);
                    traceRes = result.ToString();
                    if (result == PackerResult.OK) {
                        TcHandles.UpdateHandle(hMemPack, o);
                        traceRes += $" - {taken}, {written}";
                    }
                }

                TraceCall(TraceLevel.Verbose, traceRes);
            }
            catch (Exception ex) {
                ProcessException(ex);
            }

            return (int) result;
        }

        #endregion PackToMem

        #region DoneMemPack

        [UnmanagedCallersOnly(EntryPoint = "DoneMemPack")]
        public static int DoneMemPack(IntPtr hMemPack)
        {
            var result = PackerResult.ErrorClose;
            _callSignature = $"DoneMemPack ({hMemPack.ToString()})";
            try {
                var o = TcHandles.GetObject(hMemPack);
                if (o != null) {
                    result = Plugin.DoneMemPack(o);
                    if (o is IDisposable dip) {
                        dip.Dispose();
                    }

                    var count = TcHandles.RemoveHandle(hMemPack);
                    TraceCall(TraceLevel.Warning, $"{count} calls.");
                }
            }
            catch (Exception ex) {
                ProcessException(ex);
            }

            return (int) result;
        }

        #endregion DoneMemPack

        #region CanYouHandleThisFile

        [UnmanagedCallersOnly(EntryPoint = "CanYouHandleThisFile")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static bool CanYouHandleThisFile(IntPtr fileNamePtr)
        {
            var fileName = Marshal.PtrToStringAnsi(fileNamePtr)!;
            return CanYouHandleThisFileInternal(fileName);
        }

        [UnmanagedCallersOnly(EntryPoint = "CanYouHandleThisFileW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static bool CanYouHandleThisFileW(IntPtr fileNamePtr)
        {
            var fileName = Marshal.PtrToStringUni(fileNamePtr)!;
            return CanYouHandleThisFileInternal(fileName);
        }

        private static bool CanYouHandleThisFileInternal(string fileName)
        {
            var result = false;
            _callSignature = $"CanYouHandleThisFile ({fileName})";
            try {
                result = Plugin.CanHandleThisFile(fileName);

                TraceCall(TraceLevel.Warning, result ? "Yes" : "No");
            }
            catch (Exception ex) {
                ProcessException(ex);
            }

            return result;
        }

        #endregion CanYouHandleThisFile

        #region PackSetDefaultParams

        // PackSetDefaultParams functionality is implemented here, not included to Packer Plugin interface.
        [UnmanagedCallersOnly(EntryPoint = "PackSetDefaultParams")]
        public static void SetDefaultParams(IntPtr defParamsPtr)
        {
            var defParams = Marshal.PtrToStructure<PluginDefaultParams>(defParamsPtr);

            _callSignature = "SetDefaultParams";
            try {
                Plugin.DefaultParams = defParams;

                TraceCall(TraceLevel.Info, null);
            }
            catch (Exception ex) {
                ProcessException(ex);
            }
        }

        #endregion PackSetDefaultParams

        #region PkSetCryptCallback

        // PkSetCryptCallback & PkSetCryptCallbackW functionality is implemented here, not included to Packer Plugin interface.
        [UnmanagedCallersOnly(EntryPoint = "PkSetCryptCallback")]
        public static void SetCryptCallback(IntPtr cryptProcPtr, int cryptNumber, int flags)
        {
            var cryptProc = Marshal.GetDelegateForFunctionPointer<PkCryptCallback>(cryptProcPtr);

            _callSignature = $"PkSetCryptCallback ({cryptNumber}, {flags})";
            try {
                TcCallback.SetPackerPluginCallbacks(null, null, null, null, cryptProc, null);
                if (Plugin.PasswordManager == null) {
                    Plugin.PasswordManager = new PackerPassword(Plugin, cryptNumber, flags);
                }

                TraceCall(TraceLevel.Info, cryptProc.Method.MethodHandle.GetFunctionPointer().ToString());
            }
            catch (Exception ex) {
                ProcessException(ex);
            }
        }

        [UnmanagedCallersOnly(EntryPoint = "PkSetCryptCallbackW")]
        public static void SetCryptCallbackW(IntPtr cryptProcWPtr, int cryptNumber, int flags)
        {
            var cryptProcW = Marshal.GetDelegateForFunctionPointer<PkCryptCallbackW>(cryptProcWPtr);

            _callSignature = $"PkSetCryptCallbackW ({cryptNumber}, {flags})";
            try {
                TcCallback.SetPackerPluginCallbacks(null, null, null, null, null, cryptProcW);
                if (Plugin.PasswordManager == null) {
                    Plugin.PasswordManager = new PackerPassword(Plugin, cryptNumber, flags);
                }

                TraceCall(TraceLevel.Info, cryptProcW.Method.MethodHandle.GetFunctionPointer().ToString());
            }
            catch (Exception ex) {
                ProcessException(ex);
            }
        }

        #endregion PkSetCryptCallback

        #region GetBackgroundFlags

        // GetBackgroundFlags functionality is implemented here, not included to Packer Plugin interface.
        [UnmanagedCallersOnly(EntryPoint = "GetBackgroundFlags")]
        public static int GetBackgroundFlags()
        {
            var result = PackBackgroundFlags.None;
            _callSignature = "GetBackgroundFlags";
            try {
                result = Plugin.BackgroundFlags;

                TraceCall(TraceLevel.Info, result.ToString());
            }
            catch (Exception ex) {
                ProcessException(ex);
            }

            return (int) result;
        }

        #endregion FsGetBackgroundFlags

        #endregion Optional Methods

        #endregion Packer Plugin Exported Functions

        #region Tracing & Exceptions

        private static void ProcessException(Exception ex)
        {
            TcPluginLoader.ProcessException(_plugin, _callSignature, ex);
        }

        private static void TraceCall(TraceLevel level, string? result)
        {
            TcTrace.TraceCall(_plugin, level, _callSignature, result);
            _callSignature = null;
        }

        #endregion Tracing & Exceptions
    }
}
