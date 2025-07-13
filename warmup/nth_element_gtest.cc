#include "cpp/warmup/nth_element.hpp"

#include <optional>
#include <vector>

#include "gtest/gtest.h"

TEST(NthElementTest, EmptyArray) {
  std::vector<int> data = {};
  EXPECT_FALSE(warmup::nth_element_median(data).has_value());
}

TEST(NthElementTest, OneElementArray) {
  std::vector<int> data = {1};
  EXPECT_EQ(warmup::nth_element_median(data).value(), 1);
}

TEST(NthElementTest, TwoElementArray) {
  std::vector<int> data = {2, 1};
  // After partitioning for median (n/2=1), element at index 1 should be 2
  EXPECT_EQ(warmup::nth_element_median(data).value(), 2);
}

TEST(NthElementTest, SortedAscendingArray) {
  std::vector<int> data = {1, 2, 3, 4, 5};
  EXPECT_EQ(warmup::nth_element_median(data).value(), 3);
}

TEST(NthElementTest, SortedDescendingArray) {
  std::vector<int> data = {5, 4, 3, 2, 1};
  EXPECT_EQ(warmup::nth_element_median(data).value(), 3);
}

TEST(NthElementTest, AllEqualElements) {
  std::vector<int> data = {2, 2, 2, 2};
  EXPECT_EQ(warmup::nth_element_median(data).value(), 2);
}

TEST(NthElementTest, ArrayWithDuplicates) {
  std::vector<int> data = {3, 3, 2, 1, 3, 2};
  // sorted: 1 2 2 3 3 3, n=6, n/2=3, element at index 3 is 3
  EXPECT_EQ(warmup::nth_element_median(data).value(), 3);
}

TEST(NthElementTest, ArrayWithNegativeNumbers) {
  std::vector<int> data = {-3, 0, -1, 2, -2};
  // sorted: -3, -2, -1, 0, 2, n=5, n/2=2, element at index 2 is -1
  EXPECT_EQ(warmup::nth_element_median(data).value(), -1);
}

TEST(NthElementTest, OneElementNegative) {
  std::vector<int> data = {-5};
  EXPECT_EQ(warmup::nth_element_median(data).value(), -5);
}
