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

int64_t factorial(int64_t n) {
  int64_t res = 1;
  while (n) {
    res *= n;
    n--;
  }
  return res;
}

int64_t combinations(int64_t k, int64_t n) {
  return factorial(n) / (factorial(n - k) * factorial(k));
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  int64_t k = (n - 1) * 2;
  int64_t num = n - 1;
  std::cout << combinations(num, k) << "\n";

  return 0;
}