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

// Count number of pairs (a, b) such that
// a^2 + b = n
// a + b^2 = m
int number_of_roots(int n, int m) {
  int count = 0;
  for (int a = 0; a <= 1000; a++) {
    for (int b = 0; b <= 1000; b++) {
      if (a * a + b == n && a + b * b == m) {
        count++;
      }
    }
  }
  return count;
}

int main(int argc, char* argv[]) {
  int n, m;
  std::cin >> n >> m;
  std::cout << number_of_roots(n, m) << std::endl;

  return 0;
}
