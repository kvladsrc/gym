#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <limits>
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

int main(int argc, char *argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;

    int prev;
    std::cin >> prev;
    bool already_sorted = true;
    for (int i = 1; i < n; ++i) {
      int buf;
      std::cin >> buf;
      if (buf < prev) already_sorted = false;
      prev = buf;
    }

    int res = 0;

    for (int i = 0; i < n; ++i) {
      int buf;
      std::cin >> buf;
      res += buf;
    }

    std::cout << (already_sorted || (res > 0 && res < n) ? "Yes" : "No")
              << "\n";
  }

  return 0;
}
