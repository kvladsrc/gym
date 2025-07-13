#include "cpp/warmup/partition.hpp"

#include <vector>

#include "gtest/gtest.h"

// Helper to test both partition schemes, mimicking the original bats test logic.
void TestPartitionSchemes(std::vector<int> input, int expected_pivot,
                          const std::vector<int>& expected_array) {
  std::vector<int> mid_data = input;
  std::vector<int> right_data = input;

  int pivot_mid = warmup::partition_to_mid(mid_data);
  int pivot_right = warmup::partition_to_right(right_data);

  EXPECT_EQ(pivot_mid, expected_pivot);
  EXPECT_EQ(mid_data, expected_array);

  // The original bats test checked if both partitioning schemes produce the
  // same result.
  EXPECT_EQ(pivot_mid, pivot_right)
      << "Pivot indices from two schemes do not match.";
  EXPECT_EQ(mid_data, right_data)
      << "Resulting arrays from two schemes do not match.";
}

TEST(PartitionTest, EmptyArray) {
  TestPartitionSchemes({}, -1, {});
}

TEST(PartitionTest, OneElementArray) {
  TestPartitionSchemes({1}, 0, {1});
}

TEST(PartitionTest, TwoElementArray) {
  TestPartitionSchemes({2, 1}, 0, {1, 2});
}

TEST(PartitionTest, SortedAscendingArray) {
  TestPartitionSchemes({1, 2, 3, 4, 5}, 4, {1, 2, 3, 4, 5});
}

TEST(PartitionTest, SortedDescendingArray) {
  TestPartitionSchemes({5, 4, 3, 2, 1}, 0, {1, 4, 3, 2, 5});
}

TEST(PartitionTest, AllEqualElements) {
  TestPartitionSchemes({2, 2, 2, 2}, 0, {2, 2, 2, 2});
}

TEST(PartitionTest, ArrayWithDuplicates) {
  TestPartitionSchemes({3, 3, 2, 1, 3, 2}, 1, {1, 2, 2, 3, 3, 3});
}

TEST(PartitionTest, ArrayWithNegativeNumbers) {
  TestPartitionSchemes({-3, 0, -1, 2, -2}, 1, {-3, -2, -1, 2, 0});
}

TEST(PartitionTest, OneElementNegative) {
  TestPartitionSchemes({-5}, 0, {-5});
}
