#include <algorithm>
#include <iostream>
#include <map>
#include <random>
#include <vector>

#include "cpp/warmup/reorganize_string.hpp"
#include "gtest/gtest.h"

namespace warmup {
namespace {

using std::vector;

int CountCollisions(const vector<int>& res) {
  int collisions = 0;

  for (size_t i = 1; i < res.size(); ++i) {
    if (res[i] == res[i - 1]) {
      collisions++;
    }
  }

  return collisions;
}

int ExpectedCollisions(const vector<int>& a) {
  if (a.empty()) {
    return 0;
  }

  std::map<int, int> counts;

  for (int x : a) {
    counts[x]++;
  }

  int max_count = 0;
  for (auto const& [val, count] : counts) {
    max_count = std::max(max_count, count);
  }

  int n = a.size();

  return std::max(0, 2 * max_count - n - 1);
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

    auto res = reorganize_string(a);
    EXPECT_EQ(ExpectedCollisions(a), CountCollisions(res));
  }
}

}  // namespace
}  // namespace warmup
