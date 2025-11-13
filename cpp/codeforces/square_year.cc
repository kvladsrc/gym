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
#include <optional>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <tuple>
#include <unordered_map>
#include <vector>

using std::abs;
using std::endl;
using std::map;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

namespace {

const double precision = 0.000001;

bool good_enought(double guess, double a) {
  return abs(a - (guess * guess)) <= precision;
}

double average(double a, double b) { return (a + b) / static_cast<double>(2); }

double improve(double guess, double a) { return average(guess, a / guess); }

// Newton's method.
double my_sqrt(double a) {
  double guess = 1;

  while (!good_enought(guess, a)) {
    guess = improve(guess, a);
  }

  return guess;
}

}  // namespace

int main(int /*argc*/, char* /*argv*/[]) {
  std::ios::sync_with_stdio(false);
  std::cin.tie(nullptr);

  int t = 0;
  std::cin >> t;

  while ((t--) != 0) {
    int n = 0;
    std::cin >> n;

    if (n == 0) {
      std::cout << 0 << " " << 0 << "\n";
      continue;
    }

    int sr = static_cast<int>(std::round(my_sqrt(static_cast<double>(n))));
    if (sr * sr == n) {
      std::cout << sr - 1 << " " << 1 << "\n";
    } else {
      std::cout << -1 << "\n";
    }
  }

  return 0;
}
