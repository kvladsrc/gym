#include <algorithm>
#include <cstddef>
#include <map>
#include <random>
#include <vector>

#include "cpp/warmup/reorganize_string.hpp"
#include "gtest/gtest.h"

namespace warmup {
namespace {

using std::vector;

int CountRepetitions(const vector<int>& res) {
  int collisions = 0;

  for (std::size_t i = 1; i < res.size(); ++i) {
    if (res[i] == res[i - 1]) {
      collisions++;
    }
  }

  return collisions;
}

int ExpectedRepetitions(const vector<int>& a) {
  if (a.empty()) {
    return 0;
  }

  std::map<int, int> counts;

  for (int x : a) {
    counts[x]++;
  }

  int max_count = 0;
  for (const auto& entry : counts) {
    max_count = std::max(max_count, entry.second);
  }

  const int n = static_cast<int>(a.size());

  return std::max(0, 2 * max_count - n - 1);
}

void ExpectOptimalPermutation(const vector<int>& input) {
  const auto result = reorganize_string(input);

  EXPECT_TRUE(std::is_permutation(input.begin(), input.end(), result.begin(),
                                  result.end()));
  EXPECT_EQ(ExpectedRepetitions(input), CountRepetitions(result));
}

TEST(ReorganizeStringTest, EmptyInput) {
  EXPECT_TRUE(reorganize_string({}).empty());
}

TEST(ReorganizeStringTest, AlternatesEqualPairs) {
  EXPECT_EQ(reorganize_string({1, 1, 2, 2}), (vector<int>{1, 2, 1, 2}));
}

TEST(ReorganizeStringTest, LeavesOnlyUnavoidableRepetitions) {
  EXPECT_EQ(reorganize_string({1, 1, 1, 1, 2, 3}),
            (vector<int>{1, 2, 1, 3, 1, 1}));
}

TEST(ReorganizeStringTest, AllValuesEqual) {
  EXPECT_EQ(reorganize_string({7, 7, 7, 7}), (vector<int>{7, 7, 7, 7}));
}

TEST(ReorganizeStringTest, ExhaustiveSmallInputs) {
  constexpr std::size_t kValueCount = 4;
  constexpr std::size_t kMaxSize = 8;

  std::size_t case_count = 1;
  for (std::size_t size = 0; size <= kMaxSize; ++size) {
    for (std::size_t encoded = 0; encoded < case_count; ++encoded) {
      std::size_t value = encoded;
      vector<int> input(size);
      for (auto& element : input) {
        element = static_cast<int>(value % kValueCount);
        value /= kValueCount;
      }

      ExpectOptimalPermutation(input);
    }
    case_count *= kValueCount;
  }
}

TEST(ReorganizeStringTest, StressTest) {
  std::mt19937 rng(42);

  const int kNumTests = 10000;

  for (int t = 0; t < kNumTests; ++t) {
    int n = std::uniform_int_distribution<int>(0, 50)(rng);
    vector<int> a(n);

    for (int i = 0; i < n; ++i) {
      a[i] = std::uniform_int_distribution<int>(1, 10)(rng);
    }

    ExpectOptimalPermutation(a);
  }
}

}  // namespace
}  // namespace warmup
