#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <ctime>
#include <iomanip>
#include <iostream>
#include <numeric>
#include <queue>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::gcd;
using std::max;
using std::min;
using std::pair;
using std::priority_queue;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int64_t factorial(int64_t n) {
  int64_t result = 1;
  for (int i = 1; i <= n; i++) {
    result *= i;
  }
  return result;
}

int main(int argc, char *argv[]) {
  string want, got;
  std::cin >> want >> got;

  int want_plus = 0, got_plus = 0, got_question = 0;
  for (char c : want) {
    if (c == '+') {
      want_plus++;
    }
  }

  for (char c : got) {
    if (c == '+') {
      got_plus++;
      continue;
    }

    if (c == '?') {
      got_question++;
    }
  }

  int need_plus = want_plus - got_plus;

  double ans = 0.0;
  if (need_plus < 0 || need_plus > got_question) {
    ans = 0.0;
  } else {
    int64_t all = 1 << got_question;
    int64_t good = factorial(got_question) /
                   (factorial(got_question - need_plus) * factorial(need_plus));
    ans = static_cast<double>(good) / static_cast<double>(all);
  }

  std::cout << std::setprecision(13);
  std::cout << ans << "\n";

  return 0;
}
