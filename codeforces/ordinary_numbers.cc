#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <set>
#include <stack>
#include <string>
#include <unordered_map>
#include <vector>

using std::abs;
using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::stack;
using std::string;
using std::unordered_map;
using std::vector;

int ordinary_num(int n) {
  int copy = n;
  int digits = 0;
  while (copy) {
    digits++;
    copy /= 10;
  }

  int res = (digits - 1) * 9;
  int first_ord = 1;
  for (int i = 1; i < digits; ++i) {
    first_ord *= 10;
    first_ord += 1;
  }

  int acc = first_ord;
  while (acc <= n) {
    res++;
    acc += first_ord;
  }

  return res;
}

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    std::cout << ordinary_num(n) << "\n";
  }
  return 0;
}
