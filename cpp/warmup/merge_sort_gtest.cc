#include <gtest/gtest.h>

#include <algorithm>
#include <vector>

#include "cpp/warmup/merge_sort.hpp"

TEST(MergeSortTest, Empty) {
  std::vector<int> data = {};
  warmup::merge_sort(data);
  EXPECT_TRUE(data.empty());
}

TEST(MergeSortTest, OneElement) {
  std::vector<int> data = {1};
  warmup::merge_sort(data);
  EXPECT_EQ(data, std::vector<int>({1}));
}

TEST(MergeSortTest, AlreadySorted) {
  std::vector<int> data = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
  warmup::merge_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6, 7, 8, 9, 10}));
}

TEST(MergeSortTest, AlreadySortedReversed) {
  std::vector<int> data = {10, 9, 8, 7, 6, 5, 4, 3, 2, 1};
  warmup::merge_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6, 7, 8, 9, 10}));
}

TEST(MergeSortTest, Duplicates) {
  std::vector<int> data = {1, 3, 3, 3, 3, 3, 3, 3, 4, 2};
  warmup::merge_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 3, 3, 3, 3, 3, 3, 4}));
}

TEST(MergeSortTest, AllEqualElements) {
  std::vector<int> data = {1, 1, 1, 1, 1};
  warmup::merge_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 1, 1, 1, 1}));
}

TEST(MergeSortTest, RandomOrder) {
  std::vector<int> data = {4, 1, 3, 2, 6, 5};
  warmup::merge_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6}));
}

TEST(MergeSortTest, NegativeNumbers) {
  std::vector<int> data = {-3, -1, -2, -4, 0, 5};
  warmup::merge_sort(data);
  EXPECT_EQ(data, std::vector<int>({-4, -3, -2, -1, 0, 5}));
}

TEST(MergeSortTest, OneElementNegative) {
  std::vector<int> data = {-1};
  warmup::merge_sort(data);
  EXPECT_EQ(data, std::vector<int>({-1}));
}
