#include <gtest/gtest.h>

#include <algorithm>
#include <vector>

#include "cpp/warmup/all_divisors.hpp"

TEST(AllDivisorsTest, Zero) {
  std::vector<int> expected = {};
  std::vector<int> result = warmup::all_divisors(0);
  std::sort(result.begin(), result.end());
  EXPECT_EQ(result, expected);
}

TEST(AllDivisorsTest, One) {
  std::vector<int> expected = {1};
  std::vector<int> result = warmup::all_divisors(1);
  std::sort(result.begin(), result.end());
  EXPECT_EQ(result, expected);
}

TEST(AllDivisorsTest, Prime) {
  std::vector<int> expected = {1, 5};
  std::vector<int> result = warmup::all_divisors(5);
  std::sort(result.begin(), result.end());
  EXPECT_EQ(result, expected);
}

TEST(AllDivisorsTest, Square) {
  std::vector<int> expected = {1, 3, 9};
  std::vector<int> result = warmup::all_divisors(9);
  std::sort(result.begin(), result.end());
  EXPECT_EQ(result, expected);
}

TEST(AllDivisorsTest, Negative) {
  std::vector<int> expected = {1, 2, 3, 6};
  std::vector<int> result = warmup::all_divisors(-6);
  std::sort(result.begin(), result.end());
  EXPECT_EQ(result, expected);
}

TEST(AllDivisorsTest, Twelve) {
  std::vector<int> expected = {1, 2, 3, 4, 6, 12};
  std::vector<int> result = warmup::all_divisors(12);
  std::sort(result.begin(), result.end());
  EXPECT_EQ(result, expected);
}

TEST(AllDivisorsTest, Two) {
  std::vector<int> expected = {1, 2};
  std::vector<int> result = warmup::all_divisors(2);
  std::sort(result.begin(), result.end());
  EXPECT_EQ(result, expected);
}
