using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace RussianCertRemover.Services
{
    public class WinSqlite : IDisposable
    {
        private const string DllName = "winsqlite3.dll";

        public const int SQLITE_OK = 0;
        public const int SQLITE_ROW = 100;
        public const int SQLITE_DONE = 101;

        public const int SQLITE_INTEGER = 1;
        public const int SQLITE_FLOAT = 2;
        public const int SQLITE_TEXT = 3;
        public const int SQLITE_BLOB = 4;
        public const int SQLITE_NULL = 5;

        private static readonly IntPtr SQLITE_TRANSIENT = new IntPtr(-1);

        [DllImport(DllName, EntryPoint = "sqlite3_open", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_open([MarshalAs(UnmanagedType.LPUTF8Str)] string filename, out IntPtr db);

        [DllImport(DllName, EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_close(IntPtr db);

        [DllImport(DllName, EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_prepare_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string zSql, int nByte, out IntPtr ppStmt, IntPtr pzTail);

        [DllImport(DllName, EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_step(IntPtr pStmt);

        [DllImport(DllName, EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_finalize(IntPtr pStmt);

        [DllImport(DllName, EntryPoint = "sqlite3_column_count", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_count(IntPtr pStmt);

        [DllImport(DllName, EntryPoint = "sqlite3_column_name", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_name(IntPtr pStmt, int N);

        [DllImport(DllName, EntryPoint = "sqlite3_column_blob", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_blob(IntPtr pStmt, int iCol);

        [DllImport(DllName, EntryPoint = "sqlite3_column_bytes", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_column_bytes(IntPtr pStmt, int iCol);

        [DllImport(DllName, EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_column_text(IntPtr pStmt, int iCol);

        [DllImport(DllName, EntryPoint = "sqlite3_errmsg", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sqlite3_errmsg(IntPtr db);

        [DllImport(DllName, EntryPoint = "sqlite3_exec", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_exec(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string sql, IntPtr callback, IntPtr arg, out IntPtr errmsg);

        [DllImport(DllName, EntryPoint = "sqlite3_bind_blob", CallingConvention = CallingConvention.Cdecl)]
        private static extern int sqlite3_bind_blob(IntPtr pStmt, int index, byte[] value, int nBytes, IntPtr nDestructor);

        private IntPtr _db = IntPtr.Zero;
        public string DbPath { get; }

        public WinSqlite(string dbPath)
        {
            DbPath = dbPath;
            int rc = sqlite3_open(dbPath, out _db);
            if (rc != SQLITE_OK)
            {
                string msg = GetLastError();
                throw new InvalidOperationException($"Не удалось открыть SQLite базу '{dbPath}': {msg}");
            }
        }

        public string GetLastError()
        {
            if (_db == IntPtr.Zero) return "БД не инициализирована";
            IntPtr errPtr = sqlite3_errmsg(_db);
            return Marshal.PtrToStringUTF8(errPtr) ?? "Неизвестная ошибка SQLite";
        }

        public bool TableExists(string tableName)
        {
            string sql = "SELECT name FROM sqlite_master WHERE type='table' AND name=@t;";
            IntPtr stmt;
            int rc = sqlite3_prepare_v2(_db, sql, -1, out stmt, IntPtr.Zero);
            if (rc != SQLITE_OK) return false;

            try
            {
                byte[] tableUtf8 = Encoding.UTF8.GetBytes(tableName);
                sqlite3_bind_blob(stmt, 1, tableUtf8, tableUtf8.Length, SQLITE_TRANSIENT);
                return sqlite3_step(stmt) == SQLITE_ROW;
            }
            finally
            {
                sqlite3_finalize(stmt);
            }
        }

        public struct NssCertRow
        {
            public byte[] Id;
            public string? Nickname;
            public byte[] CertDer;
        }

        public List<NssCertRow> ReadNssCertificates()
        {
            var list = new List<NssCertRow>();
            string sql = "SELECT id, nickname, cert FROM nssCertificates;";
            IntPtr stmt;
            int rc = sqlite3_prepare_v2(_db, sql, -1, out stmt, IntPtr.Zero);
            if (rc != SQLITE_OK) return list;

            try
            {
                while (sqlite3_step(stmt) == SQLITE_ROW)
                {
                    // id (blob or text)
                    IntPtr idBlobPtr = sqlite3_column_blob(stmt, 0);
                    int idLen = sqlite3_column_bytes(stmt, 0);
                    byte[] id = new byte[idLen];
                    if (idLen > 0 && idBlobPtr != IntPtr.Zero)
                    {
                        Marshal.Copy(idBlobPtr, id, 0, idLen);
                    }

                    // nickname
                    IntPtr nickPtr = sqlite3_column_text(stmt, 1);
                    string? nickname = nickPtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(nickPtr) : null;

                    // cert (DER blob)
                    IntPtr certBlobPtr = sqlite3_column_blob(stmt, 2);
                    int certLen = sqlite3_column_bytes(stmt, 2);
                    byte[] certDer = new byte[certLen];
                    if (certLen > 0 && certBlobPtr != IntPtr.Zero)
                    {
                        Marshal.Copy(certBlobPtr, certDer, 0, certLen);
                    }

                    if (certDer.Length > 0)
                    {
                        list.Add(new NssCertRow
                        {
                            Id = id,
                            Nickname = nickname,
                            CertDer = certDer
                        });
                    }
                }
            }
            finally
            {
                sqlite3_finalize(stmt);
            }

            return list;
        }

        public bool DeleteNssCertificate(byte[] id)
        {
            // Delete from nssCertificates
            string sqlCert = "DELETE FROM nssCertificates WHERE id = @id;";
            IntPtr stmtCert;
            int rc = sqlite3_prepare_v2(_db, sqlCert, -1, out stmtCert, IntPtr.Zero);
            if (rc != SQLITE_OK) return false;

            try
            {
                sqlite3_bind_blob(stmtCert, 1, id, id.Length, SQLITE_TRANSIENT);
                if (sqlite3_step(stmtCert) != SQLITE_DONE) return false;
            }
            finally
            {
                sqlite3_finalize(stmtCert);
            }

            // Also clean from nssTrust if present
            if (TableExists("nssTrust"))
            {
                string sqlTrust = "DELETE FROM nssTrust WHERE id = @id OR certId = @id;";
                IntPtr stmtTrust;
                if (sqlite3_prepare_v2(_db, sqlTrust, -1, out stmtTrust, IntPtr.Zero) == SQLITE_OK)
                {
                    try
                    {
                        sqlite3_bind_blob(stmtTrust, 1, id, id.Length, SQLITE_TRANSIENT);
                        sqlite3_bind_blob(stmtTrust, 2, id, id.Length, SQLITE_TRANSIENT);
                        sqlite3_step(stmtTrust);
                    }
                    catch { }
                    finally
                    {
                        sqlite3_finalize(stmtTrust);
                    }
                }
            }

            return true;
        }

        public static void CreateBackup(string dbPath)
        {
            try
            {
                if (File.Exists(dbPath))
                {
                    string backupPath = $"{dbPath}.bak_{DateTime.Now:yyyyMMdd_HHmmss}";
                    File.Copy(dbPath, backupPath, true);
                    string defaultBak = $"{dbPath}.bak";
                    File.Copy(dbPath, defaultBak, true);
                }
            }
            catch { }
        }

        public void Dispose()
        {
            if (_db != IntPtr.Zero)
            {
                sqlite3_close(_db);
                _db = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }
    }
}
