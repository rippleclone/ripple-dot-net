﻿using System;

namespace Ripple.Crypto
{

    using Asn1InputStream = Org.BouncyCastle.Asn1.Asn1InputStream;
    using DerInteger = Org.BouncyCastle.Asn1.DerInteger;
    using DerSequenceGenerator = Org.BouncyCastle.Asn1.DerSequenceGenerator;
    using DerSequence = Org.BouncyCastle.Asn1.DerSequence;
    using Org.BouncyCastle.Math;
    using System.IO;

    public class ECDSASignature
	{
		/// <summary>
		/// The two components of the signature. </summary>
		public BigInteger r, s;

		/// <summary>
		/// Constructs a signature with the given components. </summary>
		public ECDSASignature(BigInteger r, BigInteger s)
		{
			this.r = r;
			this.s = s;
		}

		public static bool IsStrictlyCanonical(byte[] sig)
		{
			return CheckIsCanonical(sig, true);
		}

		public static bool CheckIsCanonical(byte[] sig, bool strict)
		{
			// Make sure signature is canonical
			// To protect against signature morphing attacks

			// Signature should be:
			// <30> <len> [ <02> <lenR> <R> ] [ <02> <lenS> <S> ]
			// where
			// 6 <= len <= 70
			// 1 <= lenR <= 33
			// 1 <= lenS <= 33

			int sigLen = sig.Length;

			if ((sigLen < 8) || (sigLen > 72))
			{
				return false;
			}

			if ((sig[0] != 0x30) || (sig[1] != (sigLen - 2)))
			{
				return false;
			}

			// Find R and check its length
			int rPos = 4, rLen = sig[rPos - 1];

			if ((rLen < 1) || (rLen > 33) || ((rLen + 7) > sigLen))
			{
				return false;
			}

			// Find S and check its length
			int sPos = rLen + 6, sLen = sig[sPos - 1];
			if ((sLen < 1) || (sLen > 33) || ((rLen + sLen + 6) != sigLen))
			{
				return false;
			}

			if ((sig[rPos - 2] != 0x02) || (sig[sPos - 2] != 0x02))
			{
				return false; // R or S have wrong type
			}

			if ((sig[rPos] & 0x80) != 0)
			{
				return false; // R is negative
			}

			if ((sig[rPos] == 0) && rLen == 1)
			{
				return false; // R is zero
			}

			if ((sig[rPos] == 0) && ((sig[rPos + 1] & 0x80) == 0))
			{
				return false; // R is padded
			}

			if ((sig[sPos] & 0x80) != 0)
			{
				return false; // S is negative
			}

			if ((sig[sPos] == 0) && sLen == 1)
			{
				return false; // S is zero
			}

			if ((sig[sPos] == 0) && ((sig[sPos + 1] & 0x80) == 0))
			{
				return false; // S is padded
			}

			byte[] rBytes = new byte[rLen];
			byte[] bytes = new byte[sLen];

			Array.Copy(sig, rPos, rBytes, 0, rLen);
			Array.Copy(sig, sPos, bytes, 0, sLen);

			BigInteger r = new BigInteger(1, rBytes), s = new BigInteger(1, bytes);

			BigInteger order = SECP256K1.Order();

			if (r.CompareTo(order) != -1 || s.CompareTo(order) != -1)
			{
				return false; // R or S greater than modulus
			}

			if (strict)
			{
				return order.Subtract(s).CompareTo(s) != -1;
			}
			else
			{
				return true;
			}

		}

		/// <summary>
		/// DER is an international standard for serializing data structures which is widely used in cryptography.
		/// It's somewhat like protocol buffers but less convenient. This method returns a standard DER encoding
		/// of the signature, as recognized by OpenSSL and other libraries.
		/// </summary>
		public  byte[] EncodeToDER()
		{
            return DERByteStream().ToArray();
		}

		public static ECDSASignature DecodeFromDER(byte[] bytes)
		{
			Asn1InputStream decoder = new Asn1InputStream(bytes);
            DerSequence seq = (DerSequence)decoder.ReadObject();
            DerInteger r, s;
			try
			{
				r = (DerInteger) seq[0];
				s = (DerInteger) seq[1];
			}
			catch (System.InvalidCastException)
			{
				return null;
			}
			finally
			{
				decoder.Close();
			}
			// OpenSSL deviates from the DER spec by interpreting these values as unsigned, though they should not be
			// Thus, we always use the positive versions. See: http://r6.ca/blog/20111119T211504Z.html
			return new ECDSASignature(r.PositiveValue, s.PositiveValue);
		}

		protected internal MemoryStream DERByteStream()
		{
			// Usually 70-72 bytes.
			MemoryStream bos = new MemoryStream(72);
			DerSequenceGenerator seq = new DerSequenceGenerator(bos);
			seq.AddObject(new DerInteger(r));
			seq.AddObject(new DerInteger(s));
			seq.Close();
			return bos;
		}
	}

}