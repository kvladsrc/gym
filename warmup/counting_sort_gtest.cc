#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/counting_sort.hpp"

TEST(CountingSortTest, Empty) {
  std::vector<int> data = {};
  warmup::counting_sort(data);
  EXPECT_TRUE(data.empty());
}

TEST(CountingSortTest, OneElement) {
  std::vector<int> data = {1};
  warmup::counting_sort(data);
  EXPECT_EQ(data, std::vector<int>({1}));
}

TEST(CountingSortTest, AlreadySorted) {
  std::vector<int> data = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
  warmup::counting_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6, 7, 8, 9, 10}));
}

TEST(CountingSortTest, AlreadySortedReversed) {
  std::vector<int> data = {10, 9, 8, 7, 6, 5, 4, 3, 2, 1};
  warmup::counting_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6, 7, 8, 9, 10}));
}

TEST(CountingSortTest, Duplicates) {
  std::vector<int> data = {1, 3, 3, 3, 3, 3, 3, 3, 4, 2};
  warmup::counting_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 3, 3, 3, 3, 3, 3, 4}));
}

TEST(CountingSortTest, AllEqualElements) {
  std::vector<int> data = {1, 1, 1, 1, 1};
  warmup::counting_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 1, 1, 1, 1}));
}

TEST(CountingSortTest, RandomOrder) {
  std::vector<int> data = {4, 1, 3, 2, 6, 5};
  warmup::counting_sort(data);
  EXPECT_EQ(data, std::vector<int>({1, 2, 3, 4, 5, 6}));
}

TEST(CountingSortTest, Zero) {
  std::vector<int> data = {0, 0, 0};
  warmup::counting_sort(data);
  EXPECT_EQ(data, std::vector<int>({0, 0, 0}));
}