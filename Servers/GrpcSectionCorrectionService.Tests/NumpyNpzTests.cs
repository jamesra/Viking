using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Viking.SectionCorrection;

namespace GrpcSectionCorrectionService.Tests
{
    /// <summary>Round-trip and malformed-input tests for <see cref="NumpyNpz"/>, which reads files written by Python.</summary>
    [TestClass]
    public class NumpyNpzTests
    {
        static byte[] NpyBytes(string header, byte[] data, byte major = 1, byte[] magic = null)
        {
            using MemoryStream ms = new();
            ms.Write(magic ?? [0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y']);
            ms.WriteByte(major);
            ms.WriteByte(0);
            byte[] h = Encoding.ASCII.GetBytes(header + "\n");
            if (major == 1)
            {
                ms.WriteByte((byte)(h.Length & 0xFF));
                ms.WriteByte((byte)(h.Length >> 8));
            }
            else
            {
                ms.Write(BitConverter.GetBytes(h.Length));
            }

            ms.Write(h);
            ms.Write(data);
            return ms.ToArray();
        }

        static Dictionary<string, NumpyArray> ReadSingle(byte[] npy)
        {
            using MemoryStream zipStream = new();
            using (ZipArchive zip = new(zipStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                using Stream es = zip.CreateEntry("a.npy").Open();
                es.Write(npy);
            }

            zipStream.Position = 0;
            return NumpyNpz.Read(zipStream);
        }

        [TestMethod]
        public void RoundTripPreservesShapesAndValues()
        {
            Dictionary<string, NumpyArray> arrays = new()
            {
                ["f"] = NumpyArray.FromFloat32([1.5f, -2f, 3f, 4f, 5f, 6f], 2, 3),
                ["v"] = NumpyArray.FromBool([true, false, true], 3),
                ["z"] = NumpyArray.ScalarInt64(42),
                ["p"] = NumpyArray.ScalarFloat64(2000.25)
            };
            using MemoryStream ms = new();
            NumpyNpz.Write(ms, arrays);
            ms.Position = 0;
            Dictionary<string, NumpyArray> back = NumpyNpz.Read(ms);

            CollectionAssert.AreEqual(new[] { 2, 3 }, back["f"].Shape);
            CollectionAssert.AreEqual(new[] { 1.5f, -2f, 3f, 4f, 5f, 6f }, back["f"].ToFloat32());
            CollectionAssert.AreEqual(new[] { true, false, true }, back["v"].ToBool());
            Assert.AreEqual(42L, back["z"].ToInt64()[0]);
            Assert.AreEqual(2000.25, back["p"].ToFloat64()[0]);
        }

        [TestMethod]
        public void ReadsPythonStyleHeaderWithTrailingCommaShape()
        {
            byte[] data = BitConverter.GetBytes(7);
            Dictionary<string, NumpyArray> back = ReadSingle(
                NpyBytes("{'descr': '<i4', 'fortran_order': False, 'shape': (1,), }", data));
            CollectionAssert.AreEqual(new[] { 1 }, back["a"].Shape);
            Assert.AreEqual(7, back["a"].ToInt32()[0]);
        }

        [TestMethod]
        public void ReadsVersion2HeaderAndZeroDimShape()
        {
            Dictionary<string, NumpyArray> back = ReadSingle(
                NpyBytes("{'descr': '<f8', 'fortran_order': False, 'shape': (), }", BitConverter.GetBytes(3.5), major: 2));
            Assert.AreEqual(0, back["a"].Shape.Length);
            Assert.AreEqual(3.5, back["a"].ToFloat64()[0]);
        }

        [TestMethod]
        public void FortranOrderIsRejected()
        {
            Assert.ThrowsExactly<NotSupportedException>(() => ReadSingle(
                NpyBytes("{'descr': '<f4', 'fortran_order': True, 'shape': (1,), }", new byte[4])));
        }

        [TestMethod]
        public void BadMagicIsInvalidData()
        {
            Assert.ThrowsExactly<InvalidDataException>(() => ReadSingle(
                NpyBytes("{'descr': '<f4', 'fortran_order': False, 'shape': (1,), }", new byte[4],
                    magic: [0, 1, 2, 3, 4, 5])));
        }

        [TestMethod]
        public void MissingShapeIsInvalidData()
        {
            Assert.ThrowsExactly<InvalidDataException>(() => ReadSingle(
                NpyBytes("{'descr': '<f4', 'fortran_order': False, }", new byte[4])));
        }

        [TestMethod]
        public void UnterminatedShapeTupleIsInvalidData()
        {
            Assert.ThrowsExactly<InvalidDataException>(() => ReadSingle(
                NpyBytes("{'descr': '<f4', 'fortran_order': False, 'shape': (3, }", new byte[4])));
        }

        [TestMethod]
        public void NonNumericOrNegativeShapeIsInvalidData()
        {
            Assert.ThrowsExactly<InvalidDataException>(() => ReadSingle(
                NpyBytes("{'descr': '<f4', 'fortran_order': False, 'shape': (x,), }", new byte[4])));
            Assert.ThrowsExactly<InvalidDataException>(() => ReadSingle(
                NpyBytes("{'descr': '<f4', 'fortran_order': False, 'shape': (-1,), }", new byte[4])));
        }

        [TestMethod]
        public void MissingDescrIsInvalidData()
        {
            Assert.ThrowsExactly<InvalidDataException>(() => ReadSingle(
                NpyBytes("{'fortran_order': False, 'shape': (1,), }", new byte[4])));
        }

        [TestMethod]
        public void NegativeVersion2HeaderLengthIsInvalidData()
        {
            using MemoryStream ms = new();
            ms.Write([0x93, (byte)'N', (byte)'U', (byte)'M', (byte)'P', (byte)'Y', 2, 0]);
            ms.Write(BitConverter.GetBytes(-5));
            Assert.ThrowsExactly<InvalidDataException>(() => ReadSingle(ms.ToArray()));
        }

        [TestMethod]
        public void WrongDescrForAccessorIsInvalidData()
        {
            NumpyArray a = NumpyArray.FromInt32([1], 1);
            Assert.ThrowsExactly<InvalidDataException>(() => a.ToFloat32());
        }
    }
}
