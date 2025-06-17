#include <iostream>
#include <utility>

namespace {

struct Matrix2x2 {
  int64_t a11, a12;
  int64_t a21, a22;

  Matrix2x2 operator*(const Matrix2x2& other) const;
};

Matrix2x2 Matrix2x2::operator*(const Matrix2x2& other) const {
  return {(a11 * other.a11) + (a12 * other.a21),
          (a11 * other.a12) + (a12 * other.a22),
          (a21 * other.a11) + (a22 * other.a21),
          (a21 * other.a12) + (a22 * other.a22)};
}

Matrix2x2 power(const Matrix2x2& a, int n) {
  // EDGE_CASE: x^0 = 1
  if (n == 0) {
    return {1, 0, 0, 1};
  }

  if (n == 1) {
    return a;
  }

  if (n % 2 == 0) {
    return power(a * a, n / 2);
  }

  return power(a, n - 1) * a;
}

int64_t fibonacci(int n) {
  Matrix2x2 fm = {0, 1, 1, 1};
  auto res = power(fm, n);
  return res.a21;
}

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  int n = 0;
  std::cin >> n;

  if (n < 0) {
    std::cout << "MUST BE POSITIVE" << "\n";
    return 0;
  }

  auto res = fibonacci(n);
  std::cout << res << "\n";

  return 0;
}
