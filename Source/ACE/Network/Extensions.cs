﻿using System;
using System.IO;

using ACE.Entity;

namespace ACE.Network
{
    public static class Extensions
    {
        private static uint CalculatePadMultiple(uint length, uint multiple) { return multiple * ((length + multiple - 1u) / multiple) - length; }
        
        public static void WriteString16L(this BinaryWriter writer, string data)
        {
            writer.Write((ushort)data.Length);
            writer.Write(data.ToCharArray());

            // client expects string length to be a multiple of 4 including the 2 bytes for length
            writer.Pad(CalculatePadMultiple(sizeof(ushort) + (uint)data.Length, 4u));
        }
        
        public static void WriteUInt16BE(this BinaryWriter writer, ushort value)
        {
            ushort beValue = (ushort)((ushort)((value & 0xFF) << 8) | ((value >> 8) & 0xFF));
            writer.Write(beValue);
        }
        
        public static void Pad(this BinaryWriter writer, uint pad) { writer.Write(new byte[pad]); }

        public static void Align(this BinaryWriter writer)
        {
            writer.Pad(CalculatePadMultiple((uint)writer.BaseStream.Length, 4u));
        }

        public static string BuildPacketString(this byte[] bytes, int startPosition = 0, int bytesToOutput = 9999)
        {
            TextWriter tw = new StringWriter();
            byte[] buffer = bytes;

            int column = 0;
            int row = 0;
            int columns = 16;
            tw.Write("   x  ");
            for (int i = 0; i < columns; i++)
            {
                tw.Write(i.ToString().PadLeft(3));
            }
            tw.WriteLine("  |Text");
            tw.Write("   0  ");

            string asciiLine = "";
            for (int i = 0; i < buffer.Length; i++)
            {
                if(column >= columns)
                {
                    row++;
                    column = 0;
                    tw.WriteLine("  |" + asciiLine);
                    asciiLine = "";
                    tw.Write((row * columns).ToString().PadLeft(4));
                    tw.Write("  ");
                }

                tw.Write(buffer[i].ToString("X2").PadLeft(3));

                if (Char.IsControl((char)buffer[i]))
                    asciiLine += " ";
                else
                    asciiLine += (char)buffer[i];
                column++;
            }

            tw.Write("".PadLeft((columns - column) * 3));
            tw.WriteLine("  |" + asciiLine);
            return tw.ToString();
        }

        public static void WritePosition(this BinaryWriter writer, uint value, long position)
        {
            long originalPosition = writer.BaseStream.Position;
            writer.BaseStream.Position = position;
            writer.Write(value);
            writer.BaseStream.Position = originalPosition;
        }

        public static ObjectGuid ReadGuid(this BinaryReader reader) { return new ObjectGuid(reader.ReadUInt32()); }

        public static void WriteGuid(this BinaryWriter writer, ObjectGuid guid) { writer.Write(guid?.Full ?? 0u); }
    }
}
