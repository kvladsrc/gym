#include <algorithm>
#include <cstdint>
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

int64_t factorial(int64_t a) {
  int64_t res = 1;
  for (int i = 1; i <= a; ++i) {
    res *= i;
  }
  return res;
}

int main(int argc, char *argv[]) {
  int a, b;
  std::cin >> a >> b;
  std::cout << factorial(min(a, b)) << "\n";
  return 0;
}
