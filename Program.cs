//Author: Eric Noga
//COPADS PROJECT 2
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static void Main(string[] args)
    {
        // Validate that there are 2 or 3 arguments.
        if (args.Length < 2 || args.Length > 3)
        {
            Console.WriteLine("Usage: dotnet run <bits> <option> <count>");
            return;
        }

        if (!int.TryParse(args[0], out int bitLength) || bitLength % 8 != 0 || bitLength < 32)
        {
            Console.WriteLine("Error: <bits> must be a multiple of 8 and at least 32.");
            return;
        }

        string option = args[1].ToLower();
        int count = (args.Length == 3 && int.TryParse(args[2], out int temp)) ? temp : 1;

        Console.WriteLine($"BitLength: {bitLength} bits");
        Stopwatch totalStopwatch = Stopwatch.StartNew();

        if (option == "prime")
        {
            PrimeGenerator.GeneratePrimes(bitLength, count);
        }
        else if (option == "odd")
        {
            OddNumberGenerator.GenerateOddNumbers(bitLength, count);
        }
        else
        {
            Console.WriteLine("Error: <option> must be 'prime' or 'odd'.");
            return;
        }

        totalStopwatch.Stop();
        Console.WriteLine($"Time to Generate: {totalStopwatch.Elapsed}");
    }
}

public static class PrimeGenerator
{
    // Shared lock object for thread-safe printing and updating the shared counter.
    private static readonly object lockObj = new();

    /// <summary>
    /// Generates the specified count of prime numbers using Task-based parallelism.
    /// </summary>
    public static void GeneratePrimes(int bitLength, int count)
    {
        int printedCount = 0;
        // Create a fixed number of worker tasks (using twice the number of processors).
        int numThreads = Environment.ProcessorCount * 2;
        CancellationTokenSource cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;
        List<Task> tasks = new List<Task>();

        for (int i = 0; i < numThreads; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                // Continuously generate candidates until cancellation is requested.
                while (!token.IsCancellationRequested)
                {
                    BigInteger candidate = GenerateRandomOddBigInteger(bitLength);

                    // Generate candidates until one passes the Miller-Rabin test.
                    while (!candidate.IsProbablyPrime())
                    {
                        if (token.IsCancellationRequested)
                            return;
                        candidate = GenerateRandomOddBigInteger(bitLength);
                    }

                    // Safely update the shared counter and print the candidate.
                    lock (lockObj)
                    {
                        if (printedCount < count)
                        {
                            printedCount++;
                            Console.WriteLine($"{printedCount}: {candidate}");
                            if (!(printedCount == count))
                                Console.WriteLine();

                            if (printedCount >= count)
                                cts.Cancel();
                        }
                    }
                }
            }, token));
        }

        // Wait for all tasks to complete. Ignore TaskCanceledExceptions.
        try
        {
            Task.WaitAll(tasks.ToArray());
        }
        catch (AggregateException ae)
        {
            ae.Handle(e => e is TaskCanceledException);
        }
    }

    /// <summary>
    /// Generates a random BigInteger of the specified bit length.
    /// Sets the highest bit to ensure the number is in the full bit-length range.
    /// </summary>
    public static BigInteger GenerateRandomBigInteger(int bitLength)
    {
        int byteLength = bitLength / 8;
        byte[] bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        // Set the highest bit.
        bytes[bytes.Length - 1] |= 0x80;
        return BigInteger.Abs(new BigInteger(bytes));
    }

    /// <summary>
    /// Generates a random odd BigInteger of the specified bit length.
    /// Ensures both the highest bit (for the correct bit-length) and the lowest bit (for oddness) are set.
    /// </summary>
    public static BigInteger GenerateRandomOddBigInteger(int bitLength)
    {
        int byteLength = bitLength / 8;
        byte[] bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        // Set the highest bit.
        bytes[bytes.Length - 1] |= 0x80;
        // Set the lowest bit so that the number is odd.
        bytes[0] |= 1;
        return BigInteger.Abs(new BigInteger(bytes));
    }

    /// <summary>
    /// Miller-Rabin Primality Test extension method.
    /// Returns true if the value is probably prime, false otherwise.
    /// </summary>
    public static bool IsProbablyPrime(this BigInteger value, int k = 10)
    {
        if (value < 2)
            return false;
        if (value == 2 || value == 3)
            return true;
        if (value % 2 == 0)
            return false;

        // Quick trial division using small primes.
        int[] smallPrimes = { 3, 5, 7, 11, 13, 17, 19, 23, 29, 31 };
        foreach (int prime in smallPrimes)
        {
            if (value == prime)
                return true;
            if (value % prime == 0)
                return false;
        }

        // Write value-1 as 2^s * d with d odd.
        BigInteger d = value - 1;
        int s = 0;
        while (d % 2 == 0)
        {
            d /= 2;
            s++;
        }

        // Perform k rounds of testing.
        for (int i = 0; i < k; i++)
        {
            BigInteger a = GenerateRandomBase(value);
            BigInteger x = BigInteger.ModPow(a, d, value);
            if (x == 1 || x == value - 1)
                continue;

            bool passed = false;
            for (int r = 0; r < s - 1; r++)
            {
                x = BigInteger.ModPow(x, 2, value);
                if (x == value - 1)
                {
                    passed = true;
                    break;
                }
            }
            if (!passed)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Generates a random base 'a' in the range [2, value - 2] for use in the Miller-Rabin test.
    /// </summary>
    private static BigInteger GenerateRandomBase(BigInteger value)
    {
        BigInteger range = value - 3;
        BigInteger randomValue = GenerateRandomBigInteger(32);
        randomValue = BigInteger.Abs(randomValue);
        return (randomValue % range) + 2;
    }
}

public static class OddNumberGenerator
{
    /// <summary>
    /// Generates the specified count of random odd numbers, computes their factor counts, and prints the results.
    /// </summary>
    public static void GenerateOddNumbers(int bitLength, int count)
    {
        for (int i = 1; i <= count; i++)
        {
            BigInteger odd = GenerateRandomOddBigInteger(bitLength);
            int factorCount = CountFactors(odd);
            if (i > 1)
                Console.WriteLine();
            Console.WriteLine($"{i}: {odd}");
            Console.WriteLine($"Number of factors: {factorCount}");
        }
    }

    /// <summary>
    /// Generates a random odd BigInteger of the specified bit length.
    /// Ensures that the highest bit is set and that the number is odd.
    /// </summary>
    public static BigInteger GenerateRandomOddBigInteger(int bitLength)
    {
        int byteLength = bitLength / 8;
        byte[] bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);
        bytes[bytes.Length - 1] |= 0x80;
        bytes[0] |= 1;
        return BigInteger.Abs(new BigInteger(bytes));
    }

    /// <summary>
    /// Counts the number of factors of the given odd number by checking only odd divisors.
    /// </summary>
    private static int CountFactors(BigInteger number)
    {
        int count = 0;
        for (BigInteger i = 1; i * i <= number; i += 2)
        {
            if (number % i == 0)
            {
                count += (i * i == number) ? 1 : 2;
            }
        }
        return count;
    }
}
