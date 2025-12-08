#include <gtest/gtest.h>

#include <vector>

#include "cpp/warmup/mo_algorithm.hpp"

using std::vector;
using warmup::query;
using warmup::solve_mo;

TEST(MoAlgorithmTest, Empty) {
  vector<int64_t> a;
  vector<query> q;
  auto res = solve_mo(0, a, q);
  EXPECT_TRUE(res.empty());
}

TEST(MoAlgorithmTest, Basic) {
  // 5 elements, 3 queries
  // array: 1 2 1 3 2
  vector<int64_t> a = {1, 2, 1, 3, 2};
  vector<query> q = {{0, 2, 0}, {1, 3, 1}, {0, 4, 2}};

  auto res = solve_mo(5, a, q);

  EXPECT_EQ(res[0], 5);
  EXPECT_EQ(res[1], 3);
  EXPECT_EQ(res[2], 9);
}

TEST(MoAlgorithmTest, TwoElementsSame) {
  vector<int64_t> a = {1, 1};
  vector<query> q = {{0, 1, 0}};
  auto res = solve_mo(2, a, q);
  ASSERT_EQ(res.size(), 1);
  EXPECT_EQ(res[0], 4);  // 1 appears twice: 2*2 = 4
}

TEST(MoAlgorithmTest, TwoElementsDifferent) {
  vector<int64_t> a = {1, 2};
  vector<query> q = {{0, 1, 0}};
  auto res = solve_mo(2, a, q);
  ASSERT_EQ(res.size(), 1);
  EXPECT_EQ(res[0], 2);  // 1*1 + 1*1 = 2
}

TEST(MoAlgorithmTest, AllEqualElements) {
  vector<int64_t> a = {7, 7, 7, 7, 7};
  vector<query> q = {
      {0, 4, 0},  // 5 sevens -> 5*5 = 25
      {2, 2, 1}   // 1 seven -> 1*1 = 1
  };
  auto res = solve_mo(5, a, q);
  ASSERT_EQ(res.size(), 2);
  EXPECT_EQ(res[0], 25);
  EXPECT_EQ(res[1], 1);
}
