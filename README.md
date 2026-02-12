# FileSorting

External merge sort for large text files (~100GB) in C#.

Sorts files in the format `<Number>. <String>` by string (alphabetically), then by number (ascending).

```
Input:                      Output:
415. Apple                  1. Apple
30432. Something            415. Apple
1. Apple                    2. Banana is yellow
32. Cherry is the best      32. Cherry is the best
2. Banana is yellow         30432. Something
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Quick Start

```bash
# Generate a test file
dotnet run --project src/Generator/Generator.csproj -c Release -- --size 1GB --output test.txt

# Sort it
dotnet run --project src/Sorter/Sorter.csproj -c Release -- --input test.txt --output sorted.txt
```

## Generator

Creates test files with configurable size. Uses a built-in dictionary of ~700 words/phrases with duplicate string parts for realistic sorting workloads.

```bash
dotnet run --project src/Generator/Generator.csproj -c Release -- \
  --size 1GB \
  --output test.txt \
  --dictionary ./custom-words.txt \  # optional
  --seed 42                           # optional, for reproducibility
```

| Option | Description |
|--------|-------------|
| `-s, --size` | Target file size (e.g., `500MB`, `10GB`) |
| `-o, --output` | Output file path |
| `-d, --dictionary` | Custom word list file (one word per line) |
| `--seed` | RNG seed for reproducible output |

**Performance**: ~1 GB/s+ on NVMe (parallel generation across all cores).

## Sorter

External merge sort that handles files larger than available memory.

```bash
dotnet run --project src/Sorter/Sorter.csproj -c Release -- \
  --input test.txt \
  --output sorted.txt \
  --chunk-size 256MB \    # optional
  --temp-dir /tmp \       # optional
  --parallel 8            # optional
```

| Option | Description |
|--------|-------------|
| `-i, --input` | Input file path |
| `-o, --output` | Output file path |
| `-c, --chunk-size` | Chunk size for splitting (default: auto, min 64MB) |
| `-t, --temp-dir` | Directory for temp files (default: system temp) |
| `-p, --parallel` | Max parallelism (default: CPU core count) |

### How It Works

**Phase 1 - Chunk & Sort**: The input file is split into memory-sized chunks at newline boundaries. Each chunk is read, parsed into zero-allocation `ParsedLine` structs (byte offsets, no string copies), sorted in parallel, and written to LZ4-compressed temp files.

**Phase 2 - K-Way Merge**: Sorted chunks are merged using a tournament loser tree. For large numbers of chunks, multi-pass merging runs parallel batch merges. The final output is always written uncompressed.

### Sorter (DuckDB Alternative)

An alternative sorter using DuckDB's built-in parallel radix sort engine.

```bash
dotnet run --project src/SorterDuckDb/SorterDuckDb.csproj -c Release -- \
  --input test.txt \
  --output sorted.txt
```

## Performance Techniques

| Technique | Where |
|-----------|-------|
| Zero-alloc `Span<byte>` parsing | `ParsedLine` stores offsets into byte buffers |
| `ArrayPool<byte>.Shared` | All temporary buffers (chunks, lines, merge output) |
| `Utf8Parser.TryParse` | Number parsing without string allocation |
| `SequenceCompareTo` | SIMD-accelerated ordinal byte comparison |
| `System.IO.Pipelines` | PipeReader for chunking and merge reads |
| Tournament loser tree | log2(k) comparisons per merge step (vs 2*log2(k) for heap) |
| LZ4 compression | Temp chunk files compressed at >500 MB/s, 2-3x smaller |
| Server GC | Per-core heaps for parallel garbage collection |
| Parallel chunk sorting | One sort task per CPU core via `Parallel.ForEachAsync` |
| Parallel batch merging | Independent merge batches processed concurrently |

## Project Structure

```
FileSorting/
├── src/
│   ├── Shared/              # Size parser, common utilities
│   ├── Generator/           # Parallel test file generator
│   ├── Sorter/              # External merge sort
│   └── SorterDuckDb/        # DuckDB-based alternative
├── tests/
│   ├── Shared.Tests/
│   ├── Generator.Tests/
│   └── Sorter.Tests/        # Unit + integration tests
└── benchmarks/
    ├── Sorter.Benchmarks/
    └── Generator.Benchmarks/
```

## Tests

```bash
dotnet test
```

## Benchmarks

```bash
dotnet run --project benchmarks/Sorter.Benchmarks -c Release
```
