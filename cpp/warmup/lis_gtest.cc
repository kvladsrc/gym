#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/lis.hpp"

TEST(LISTest, EmptyArray) {
  std::vector<int> data = {};
  EXPECT_EQ(warmup::lis(data), 0);
}

TEST(LISTest, OneElement) {
  std::vector<int> data = {1};
  EXPECT_EQ(warmup::lis(data), 1);
}

TEST(LISTest, Decreasing) {
  std::vector<int> data = {10, 9, 8, 7, 6, 5, 4, 3, 2, 1};
  EXPECT_EQ(warmup::lis(data), 1);
}

TEST(LISTest, Increasing) {
  std::vector<int> data = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
  EXPECT_EQ(warmup::lis(data), 10);
}

TEST(LISTest, Duplicates) {
  std::vector<int> data = {1, 1, 1, 1, 1, 1, 1, 1, 1, 1};
  EXPECT_EQ(warmup::lis(data), 1);
}

TEST(LISTest, DuplicatesTwoElements) {
  std::vector<int> data = {1, 1, 1, 1, 1, 2, 2, 2, 2, 2};
  EXPECT_EQ(warmup::lis(data), 2);
}

TEST(LISTest, DuplicatesTwoElementsDecreasing) {
  std::vector<int> data = {2, 2, 2, 2, 2, 1, 1, 1, 1, 1};
  EXPECT_EQ(warmup::lis(data), 1);
}

TEST(LISTest, Mixed) {
  std::vector<int> data = {7, 5, 3, 7, 9, 6, 34, 6, 8, 5};
  EXPECT_EQ(warmup::lis(data), 4);
}

TEST(LISTest, AllEqualElements) {
  std::vector<int> data = {2, 2, 2, 2, 2};
  EXPECT_EQ(warmup::lis(data), 1);
}

TEST(LISTest, AlternatingHighLow) {
  std::vector<int> data = {1, 3, 2, 4, 3, 5};
  EXPECT_EQ(warmup::lis(data), 4);
}

TEST(LISTest, Zigzag) {
  std::vector<int> data = {1, 3, 2, 4, 3, 5, 4, 6, 5};
  EXPECT_EQ(warmup::lis(data), 5);
}

TEST(LISTest, MultipleEqualLISLengths) {
  std::vector<int> data = {3, 10, 2, 1, 20, 4};
  EXPECT_EQ(warmup::lis(data), 3);
}

TEST(LISTest, LongIncreasingWithNoise) {
  std::vector<int> data = {1, 2, 3, 2, 4, 5, 4, 6, 7, 5, 8, 9, 6, 10, 11};
  EXPECT_EQ(warmup::lis(data), 11);
}

TEST(LISTest, MaximalLISAtEnd) {
  std::vector<int> data = {10, 9, 8, 1, 2, 3, 4};
  EXPECT_EQ(warmup::lis(data), 4);
}

TEST(LISTest, MaximalLISAtBeginning) {
  std::vector<int> data = {1, 2, 3, 4, 10, 9, 8};
  EXPECT_EQ(warmup::lis(data), 5);
}

TEST(LISTest, WithNegativeNumbers) {
  std::vector<int> data = {-5, -1, -3, 0, 2, -2, 3};
  EXPECT_EQ(warmup::lis(data), 5);
}
