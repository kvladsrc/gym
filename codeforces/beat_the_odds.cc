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
    int even_count = 0, odd_count = 0;
    while (n--) {
      int buf;
      std::cin >> buf;

      if (buf % 2) {
        odd_count++;
      } else {
        even_count++;
      }
    }

    std::cout << min(even_count, odd_count) << "\n";
  }

  return 0;
}
