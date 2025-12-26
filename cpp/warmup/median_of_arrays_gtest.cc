#include <vector>

#include "cpp/warmup/median_of_arrays.hpp"
#include "gtest/gtest.h"

namespace warmup {

TEST(MedianOfArraysTest, BasicTest) {
  std::vector<int> a = {1, 3, 5};
  std::vector<int> b = {2, 4, 6};
  // Merged: 1, 2, 3, 4, 5, 6
  // Indices: 0, 1, 2, 3, 4, 5
  EXPECT_EQ(nth_of_arrays(a, b, 0), 1);
  EXPECT_EQ(nth_of_arrays(a, b, 1), 2);
  EXPECT_EQ(nth_of_arrays(a, b, 2), 3);
  EXPECT_EQ(nth_of_arrays(a, b, 3), 4);
  EXPECT_EQ(nth_of_arrays(a, b, 4), 5);
  EXPECT_EQ(nth_of_arrays(a, b, 5), 6);
}

TEST(MedianOfArraysTest, DifferentSizes) {
  std::vector<int> a = {1, 2};
  std::vector<int> b = {3, 4, 5, 6};
  // Merged: 1, 2, 3, 4, 5, 6
  EXPECT_EQ(nth_of_arrays(a, b, 2), 3);
  EXPECT_EQ(nth_of_arrays(a, b, 4), 5);
}

TEST(MedianOfArraysTest, EmptyArray) {
  std::vector<int> a = {};
  std::vector<int> b = {1, 2, 3};
  EXPECT_EQ(nth_of_arrays(a, b, 0), 1);
  EXPECT_EQ(nth_of_arrays(a, b, 1), 2);
  EXPECT_EQ(nth_of_arrays(a, b, 2), 3);
}

TEST(MedianOfArraysTest, OutOfBounds) {
  std::vector<int> a = {1};
  std::vector<int> b = {2};
  EXPECT_EQ(nth_of_arrays(a, b, 2), -1);
}

TEST(MedianOfArraysTest, DisjointArrays) {
  std::vector<int> a = {1, 2, 3};
  std::vector<int> b = {10, 11, 12};
  // Merged: 1, 2, 3, 10, 11, 12
  EXPECT_EQ(nth_of_arrays(a, b, 2), 3);
  EXPECT_EQ(nth_of_arrays(a, b, 3), 10);
}

TEST(MedianOfArraysTest, OverlappingArrays) {
  std::vector<int> a = {1, 5, 10};
  std::vector<int> b = {2, 6, 11};
  // Merged: 1, 2, 5, 6, 10, 11
  EXPECT_EQ(nth_of_arrays(a, b, 0), 1);
  EXPECT_EQ(nth_of_arrays(a, b, 1), 2);
  EXPECT_EQ(nth_of_arrays(a, b, 2), 5);
  EXPECT_EQ(nth_of_arrays(a, b, 3), 6);
  EXPECT_EQ(nth_of_arrays(a, b, 4), 10);
  EXPECT_EQ(nth_of_arrays(a, b, 5), 11);
}

TEST(MedianOfArraysTest, RightBranchCoverage) {
  std::vector<int> a = {10, 20};
  std::vector<int> b = {1, 2};
  // Merged: 1, 2, 10, 20
  // Index 1 is 2.
  // This case triggers r = m - 1 because a[m] (10) > b[need_b] (1) is not true,
  // but we need to go left in 'a' because the elements in 'a' are too large.
  // Actually, wait, let's re-verify the logic in the thought process.
  // m=1, a[1]=20, b[1]=2. min(20, 2) = 2. max(10, 1) = 10. 2 >= 10 False.
  // a[1] (20) < b[1] (2) False.
  // Else -> r = m - 1. Correct.
  EXPECT_EQ(nth_of_arrays(a, b, 1), 2);
}

}  // namespace warmup
