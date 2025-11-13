#include <gtest/gtest.h>

#include <cstdint>

#include "cpp/warmup/fast_power.hpp"

TEST(FastPowerTest, SmallExponent) {
  EXPECT_EQ(warmup::fast_power(2, 3, warmup::modulo), 8);
}

TEST(FastPowerTest, ExponentZero) {
  EXPECT_EQ(warmup::fast_power(5, 0, warmup::modulo), 1);
}

TEST(FastPowerTest, BaseZero) {
  EXPECT_EQ(warmup::fast_power(0, 5, warmup::modulo), 0);
}

TEST(FastPowerTest, LargeExponent) {
  EXPECT_EQ(warmup::fast_power(2, 30, warmup::modulo), 73741817);
}

TEST(FastPowerTest, LargeBase) {
  EXPECT_EQ(warmup::fast_power(123456, 2, warmup::modulo), 241383831);
}

TEST(FastPowerTest, ModuloOperationCheck) {
  EXPECT_EQ(warmup::fast_power(1000000006, 2, warmup::modulo), 1);
}

TEST(FastPowerTest, OnePowerAnyExponent) {
  EXPECT_EQ(warmup::fast_power(1, 100000, warmup::modulo), 1);
}

TEST(FastPowerTest, MaximumModuloCheck) {
  EXPECT_EQ(warmup::fast_power(2, 1000000000, warmup::modulo), 140625001);
}

TEST(FastPowerTest, IdentityModuloCheck) {
  EXPECT_EQ(warmup::fast_power(1000000007, 5, warmup::modulo), 0);
}

TEST(FastPowerTest, PowerOfZeroToZero) {
  EXPECT_EQ(warmup::fast_power(0, 0, warmup::modulo), 1);
}
