#include <gtest/gtest.h>

#include <numeric>
#include <vector>

#include "cpp/warmup/permutations.hpp"

using std::vector;
using warmup::permutations_heaps;

TEST(PermutationsTest, Basic) {
  int n = 3;
  vector<int> arr(n);
  std::iota(arr.begin(), arr.end(), 0);

  auto count = permutations_heaps(arr, n);
  EXPECT_EQ(count, 6);  // 3! = 6
}

TEST(PermutationsTest, SingleElement) {
  int n = 1;
  vector<int> arr = {0};
  auto count = permutations_heaps(arr, n);
  EXPECT_EQ(count, 1);
}

TEST(PermutationsTest, FourElements) {
  int n = 4;
  vector<int> arr(n);
  std::iota(arr.begin(), arr.end(), 0);
  auto count = permutations_heaps(arr, n);
  EXPECT_EQ(count, 24);  // 4! = 24
}
