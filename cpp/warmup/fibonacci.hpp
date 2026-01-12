#ifndef CPP_WARMUP_FIBONACCI_H_
#define CPP_WARMUP_FIBONACCI_H_

#include <cstdint>

namespace warmup {

struct Matrix2x2 {
  uint64_t a11, a12;
  uint64_t a21, a22;

  Matrix2x2 operator*(const Matrix2x2& other) const;
};

Matrix2x2 power(const Matrix2x2& a, int n);
int64_t fibonacci(int n);

}  // namespace warmup

#endif  // CPP_WARMUP_FIBONACCI_H_
