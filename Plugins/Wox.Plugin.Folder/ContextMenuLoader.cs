using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Wox.Infrastructure.Logger;

namespace Wox.Plugin.Folder
{
    internal class ContextMenuLoader : IContextMenu
    {
        private readonly PluginInitContext _context;

        public ContextMenuLoader(PluginInitContext context)
        {
            _context = context;
        }

        public List<Result> LoadContextMenus(Result selectedResult)
        {
            var contextMenus = new List<Result>();
            if (selectedResult.ContextData is SearchResult record)
            {
                if (record.Type == ResultType.File)
                {
                    contextMenus.Add(CreateOpenWithEditorResult(record));
                    contextMenus.Add(CreateOpenContainingFolderResult(record));
                }

                var icoPath = (record.Type == ResultType.File) ? Main.FileImagePath : Main.FolderImagePath;
                var fileOrFolder = (record.Type == ResultType.File) ? "file" : "folder";
                contextMenus.Add(new Result
                {
                    Title = "Copy Path",
                    SubTitle = $"Copy the path of {fileOrFolder} into the clipboard",
                    Action = (context) =>
                    {
                        try
                        {
                            Clipboard.SetText(record.FullPath);
                            return true;
                        }
                        catch (Exception e)
                        {
                            var message = "Fail to set text in clipboard";
                            LogException(message, e);
                            _context.API.ShowMsg(message);
                            return false;
                        }
                    },
                    IcoPath = icoPath
                });

                contextMenus.Add(new Result
                {
                    Title = "Copy",
                    SubTitle = $"Copy the {fileOrFolder} to the clipboard",
                    Action = (context) =>
                    {
                        try
                        {
                            Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { record.FullPath });
                            return true;
                        }
                        catch (Exception e)
                        {
                            var message = $"Fail to set {fileOrFolder} in clipboard";
                            LogException(message, e);
                            _context.API.ShowMsg(message);
                            return false;
                        }
                        
                    },
                    IcoPath = icoPath
                });

                if (record.Type == ResultType.File || record.Type == ResultType.Folder)
                    contextMenus.Add(new Result
                    {
                        Title = "Delete",
                        Action = (context) =>
                        {
                            try
                            {
                                if (record.Type == ResultType.File)
                                    File.Delete(record.FullPath);
                                else
                                    Directory.Delete(record.FullPath);
                            }
                            catch(Exception e)
                            {
                                var message = $"Fail to delete {fileOrFolder} at {record.FullPath}";
                                LogException(message, e);
                                _context.API.ShowMsg(message);
                                return false;
                            }

                            return true;
                        },
                        IcoPath = icoPath
                    });

            }

            return contextMenus;
        }

        private Result CreateOpenContainingFolderResult(SearchResult record)
        {
            return new Result
            {
                Title = "Open containing folder",
                Action = _ =>
                {
                    try
                    {
                        Process.Start("explorer.exe", $" /select,\"{record.FullPath}\"");
                    }
                    catch(Exception e)
                    {
                        var message = $"Fail to open file at {record.FullPath}";
                        LogException(message, e);
                        _context.API.ShowMsg(message);
                        return false;
                    }

                    return true;
                },
                IcoPath = Main.FolderImagePath
            };
        }


        private Result CreateOpenWithEditorResult(SearchResult record)
        {
            string editorPath = "notepad.exe"; // TODO add the ability to create a custom editor

            var name = "Open With Editor: " + Path.GetFileNameWithoutExtension(editorPath);
            return new Result
            {
                Title = name,
                Action = _ =>
                {
                    try
                    {
                        Process.Start(editorPath, record.FullPath);
                        return true;
                    }
                    catch (Exception e)
                    {
                        var message = $"Fail to editor for file at {record.FullPath}";
                        LogException(message, e);
                        _context.API.ShowMsg(message);
                        return false;
                    }
                },
                IcoPath = editorPath
            };
        }

        public void LogException(string message, Exception e)
        {
            Log.Exception($"|Wox.Plugin.Folder.ContextMenu|{message}", e);
        }
    }

    public class SearchResult
    {
        public string FullPath { get; set; }
        public ResultType Type { get; set; }
    }

    public enum ResultType
    {
        Volume,
        Folder,
        File
    }
}