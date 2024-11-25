#include <algorithm>
#include <climits>
#include <cmath>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <map>
#include <memory>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::sqrt;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

const double precision = 0.0001;

bool good_enought(double guess, double a) {
  return abs(a - (guess * guess)) <= precision;
}

double average(double a, double b) { return (a + b) / 2; }

double improve(double guess, double a) { return average(guess, a / guess); }

// Newton's method.
double sqrt(double a) {
  double guess = 1;

  while (!good_enought(guess, a)) {
    guess = improve(guess, a);
  }

  return guess;
}

int main(int argc, char *argv[]) {
  double a;
  std::cin >> a;
  std::cout << sqrt(a) << "\n";

  return 0;
}
