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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n;
    std::cin >> n;
    int min_val = 10;
    int prod = 1;
    int zeroes = 0;
    while (n--) {
      int buf;
      std::cin >> buf;

      if (!buf) {
        zeroes++;
        continue;
      }

      if (buf < min_val) min_val = buf;
      prod *= buf;
    }

    if (zeroes > 1) {
      std::cout << 0 << "\n";
      continue;
    }

    if (zeroes == 1) {
      std::cout << prod << "\n";
      continue;
    }

    std::cout << prod / min_val * (min_val + 1) << "\n";
  }
  return 0;
}
