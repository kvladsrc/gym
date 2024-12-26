#include <cstdlib>
#include <iostream>
#include <string>

using std::abs;
using std::string;

const double precision = 0.0001;

static bool good_enought(double guess, double a) {
  return abs(a - (guess * guess)) <= precision;
}

static double average(double a, double b) { return (a + b) / 2; }

static double improve(double guess, double a) {
  return average(guess, a / guess);
}

// Newton's method.
static double sqrt(double a) {
  double guess = 1;

  while (!good_enought(guess, a)) {
    guess = improve(guess, a);
  }

  return guess;
}

int main(int /*argc*/, char* /*argv*/[]) {
  double a = 0;
  std::cin >> a;
  std::cout << sqrt(a) << "\n";

  return 0;
}
