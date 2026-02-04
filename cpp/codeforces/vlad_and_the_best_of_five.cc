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
    int a_count = 0, b_count = 0;
    for (int i = 0; i < 5; ++i) {
      char c;
      std::cin >> c;
      if (c == 'A') {
        ++a_count;
      } else {
        ++b_count;
      }
    }

    std::cout << (a_count > b_count ? 'A' : 'B') << std::endl;
  }
  return 0;
}
