#include <algorithm>
#include <iostream>
#include <set>
#include <string>
#include <unordered_map>
#include <vector>

using std::max;
using std::min;
using std::pair;
using std::set;
using std::sort;
using std::string;
using std::unordered_map;
using std::vector;

#define MAX_SIZE 1000000

bool t_num(int64_t n) {
  int64_t guess = 2;
  while ((guess * guess) <= n) {
    if (guess * guess == n) return true;
    if (!(n % guess)) return false;
    guess++;
  }
  return false;  // prime
}

set<int64_t> *precalculate() {
  auto precalculated = new set<int64_t>;
  vector<bool> eratosthenes(MAX_SIZE);
  for (int64_t i = 2; i < MAX_SIZE; ++i) {
    if (!eratosthenes[i]) {
      precalculated->insert(i * i);
      int pos = i + i;
      while (pos < MAX_SIZE) {
        eratosthenes[pos] = true;
        pos += i;
      }
    } else {
      continue;
    }
  }

  return precalculated;
}

int main(int argc, char *argv[]) {
  int n;
  std::cin >> n;
  vector<int64_t> nums(n);
  for (int i = 0; i < n; ++i) {
    std::cin >> nums[i];
  }

  auto precalc = precalculate();

  for (int64_t num : nums) {
    auto it = precalc->find(num);
    if (it != precalc->end()) {
      std::cout << "YES"
                << "\n";
    } else {
      std::cout << "NO"
                << "\n";
    }
  }

  return 0;
}
