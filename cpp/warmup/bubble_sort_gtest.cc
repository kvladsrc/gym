#include <gtest/gtest.h>

#include <algorithm>
#include <vector>

#include "cpp/warmup/bubble_sort.hpp"

TEST(BubbleSortTest, Empty) {
  std::vector<int> data = {};
  warmup::bubble_sort(data);
  EXPECT_TRUE(data.empty());
}

TEST(BubbleSortTest, OneElement) {
  std::vector<int> data = {1};
  warmup::bubble_sort(data);
  EXPECT_EQ(data, std::vector<int>({1}));
}

TEST(BubbleSortTest, AlreadySorted) {
  std::vector<int> data = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
  warmup::bubble_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6, 7, 8, 9, 10}));
}

TEST(BubbleSortTest, AlreadySortedReversed) {
  std::vector<int> data = {10, 9, 8, 7, 6, 5, 4, 3, 2, 1};
  warmup::bubble_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6, 7, 8, 9, 10}));
}

TEST(BubbleSortTest, Duplicates) {
  std::vector<int> data = {1, 3, 3, 3, 3, 3, 3, 3, 4, 2};
  warmup::bubble_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 3, 3, 3, 3, 3, 3, 4}));
}

TEST(BubbleSortTest, AllEqualElements) {
  std::vector<int> data = {1, 1, 1, 1, 1};
  warmup::bubble_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 1, 1, 1, 1}));
}

TEST(BubbleSortTest, RandomOrder) {
  std::vector<int> data = {4, 1, 3, 2, 6, 5};
  warmup::bubble_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6}));
}

TEST(BubbleSortTest, NegativeNumbers) {
  std::vector<int> data = {-3, -1, -2, -4, 0, 5};
  warmup::bubble_sort(data);
  EXPECT_EQ(data, std::vector<int>({-4, -3, -2, -1, 0, 5}));
}

TEST(BubbleSortTest, OneElementNegative) {
  std::vector<int> data = {-1};
  warmup::bubble_sort(data);
  EXPECT_EQ(data, std::vector<int>({-1}));
}
