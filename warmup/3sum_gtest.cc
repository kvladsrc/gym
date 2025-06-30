#include <algorithm>
#include <vector>

#include "cpp/warmup/3sum.hpp"
#include "gtest/gtest.h"

TEST(ThreeSumTest, EmptyArray) {
  std::vector<int> data = {};
  EXPECT_FALSE(warmup::three_sum(data, 0));
}

TEST(ThreeSumTest, LessThanThreeElements) {
  std::vector<int> data = {1, 1};
  EXPECT_FALSE(warmup::three_sum(data, 3));
}

TEST(ThreeSumTest, ThreeElementsSumExists) {
  std::vector<int> data = {1, 2, 3};
  std::sort(data.begin(), data.end());
  EXPECT_TRUE(warmup::three_sum(data, 6));
}

TEST(ThreeSumTest, ThreeElementsSumNotExists) {
  std::vector<int> data = {1, 2, 4};
  std::sort(data.begin(), data.end());
  EXPECT_FALSE(warmup::three_sum(data, 8));
}

TEST(ThreeSumTest, Duplicates) {
  std::vector<int> data = {2, 2, 3, 2};
  std::sort(data.begin(), data.end());
  EXPECT_TRUE(warmup::three_sum(data, 7));
}

TEST(ThreeSumTest, AllEqualElements) {
  std::vector<int> data = {2, 2, 2, 2, 2};
  std::sort(data.begin(), data.end());
  EXPECT_TRUE(warmup::three_sum(data, 6));
}

TEST(ThreeSumTest, NegativeNumbers) {
  std::vector<int> data = {-1, 0, 1};
  std::sort(data.begin(), data.end());
  EXPECT_TRUE(warmup::three_sum(data, 0));
}

TEST(ThreeSumTest, WithZeroSum) {
  std::vector<int> data = {-5, -3, 2, 8};
  std::sort(data.begin(), data.end());
  EXPECT_TRUE(warmup::three_sum(data, 0));
}

TEST(ThreeSumTest, LargeNumbers) {
  std::vector<int> data = {1000000000, 1000000000, -1000000000, 0};
  std::sort(data.begin(), data.end());
  EXPECT_TRUE(warmup::three_sum(data, 1000000000));
}

TEST(ThreeSumTest, MultipleTriplets) {
  std::vector<int> data = {1, 2, 3, 4, 5, 6};
  std::sort(data.begin(), data.end());
  EXPECT_TRUE(warmup::three_sum(data, 9));
}
