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

    int case1 = n + ((7 - n % 7) % 7);
    int case2 = n - n % 7;

    if ((case1 / 10) % 10 == (n / 10) % 10) {
      std::cout << case1 << "\n";
    } else {
      std::cout << case2 << "\n";
    }
  }

  return 0;
}
