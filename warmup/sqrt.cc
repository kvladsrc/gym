#include <cstdlib>
#include <iostream>
#include <limits>
#include <string>

using std::abs;
using std::string;

namespace {

const double precision = 0.0001;

bool good_enought(double guess, double a) {
  return abs(a - (guess * guess)) <= precision;
}

double average(double a, double b) { return (a + b) / 2; }

double improve(double guess, double a) { return average(guess, a / guess); }

// Newton's method.
double sqrt(double a) {
  if (a == 0) {
    return 0;
  }

  if (a < 0) {
    return std::numeric_limits<double>::quiet_NaN();
  }

  double guess = 1;

  while (!good_enought(guess, a)) {
    guess = improve(guess, a);
  }

  return guess;
}

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  double a = 0;
  std::cin >> a;
  std::cout << sqrt(a) << "\n";

  return 0;
}
