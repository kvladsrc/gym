#include <gtest/gtest.h>

#include <algorithm>
#include <vector>

#include "cpp/warmup/quick_sort.hpp"

TEST(QuickSortTest, Empty) {
  std::vector<int> data = {};
  warmup::quick_sort(data, 0, -1);
  EXPECT_TRUE(data.empty());
}

TEST(QuickSortTest, OneElement) {
  std::vector<int> data = {1};
  warmup::quick_sort(data, 0, 0);
  EXPECT_EQ(data, std::vector<int>({1}));
}

TEST(QuickSortTest, AlreadySorted) {
  std::vector<int> data = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
  warmup::quick_sort(data, 0, data.size() - 1);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6, 7, 8, 9, 10}));
}

TEST(QuickSortTest, AlreadySortedReversed) {
  std::vector<int> data = {10, 9, 8, 7, 6, 5, 4, 3, 2, 1};
  warmup::quick_sort(data, 0, data.size() - 1);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6, 7, 8, 9, 10}));
}

TEST(QuickSortTest, Duplicates) {
  std::vector<int> data = {1, 3, 3, 3, 3, 3, 3, 3, 4, 2};
  warmup::quick_sort(data, 0, data.size() - 1);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 3, 3, 3, 3, 3, 3, 4}));
}

TEST(QuickSortTest, AllEqualElements) {
  std::vector<int> data = {1, 1, 1, 1, 1};
  warmup::quick_sort(data, 0, data.size() - 1);
  EXPECT_EQ(data, std::vector<int>({1, 1, 1, 1, 1}));
}

TEST(QuickSortTest, RandomOrder) {
  std::vector<int> data = {4, 1, 3, 2, 6, 5};
  warmup::quick_sort(data, 0, data.size() - 1);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6}));
}

TEST(QuickSortTest, NegativeNumbers) {
  std::vector<int> data = {-3, -1, -2, -4, 0, 5};
  warmup::quick_sort(data, 0, data.size() - 1);
  EXPECT_EQ(data, std::vector<int>({-4, -3, -2, -1, 0, 5}));
}

TEST(QuickSortTest, OneElementNegative) {
  std::vector<int> data = {-1};
  warmup::quick_sort(data, 0, 0);
  EXPECT_EQ(data, std::vector<int>({-1}));
}
