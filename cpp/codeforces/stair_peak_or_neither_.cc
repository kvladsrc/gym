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
    int a, b, c;
    std::cin >> a >> b >> c;

    if (a < b && b < c) {
      std::cout << "STAIR"
                << "\n";
    } else if (a < b && b > c) {
      std::cout << "PEAK"
                << "\n";
    } else {
      std::cout << "NONE"
                << "\n";
    }
  }

  return 0;
}
