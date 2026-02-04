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

int main(int argc, char* argv[]) {
  int t;
  std::cin >> t;
  while (t--) {
    int n, x;
    std::cin >> n >> x;

    int odd_count = 0;
    int even_count = 0;
    while (n--) {
      int buf;
      std::cin >> buf;

      if (buf % 2) {
        odd_count++;
      } else {
        even_count++;
      }
    }

    if (odd_count == 0) {
      std::cout << "NO"
                << "\n";
      continue;
    }

    odd_count--;
    x--;
    while (x) {
      if (x >= 2 && odd_count >= 2) {
        x -= 2;
        odd_count -= 2;
        continue;
      }

      if (even_count) {
        even_count--;
        x--;
        continue;
      }

      break;
    }

    if (x) {
      std::cout << "NO"
                << "\n";
    } else {
      std::cout << "YES"
                << "\n";
    }
  }

  return 0;
}
