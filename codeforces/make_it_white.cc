#include <algorithm>
#include <cstdlib>
#include <ctime>
#include <iostream>
#include <numeric>
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
    int first = n + 1;
    int last = 0;
    for (int i = 1; i <= n; ++i) {
      char c;
      std::cin >> c;
      if (c == 'B') {
        if (i < first) first = i;
        if (i > last) last = i;
      }
    }
    std::cout << last - first + 1 << "\n";
  }
  return 0;
}
