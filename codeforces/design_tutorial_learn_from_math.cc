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

#define MAX_NUM 1000000

vector<bool> eratosthenes() {
  vector<bool> res(MAX_NUM);
  for (int i = 2; i < MAX_NUM; ++i) {
    if (res[i]) continue;

    int pos = i + i;
    while (pos < MAX_NUM) {
      res[pos] = true;
      pos += i;
    }
  }

  return res;
}

bool is_prime(vector<bool> &erato_table, int num) { return !erato_table[num]; }

int main(int argc, char *argv[]) {
  auto erato = eratosthenes();

  int n;
  std::cin >> n;

  // Because of theorem we never reach i >= n / 2.
  for (int i = 2; i < n; ++i) {
    if (!is_prime(erato, i) && !is_prime(erato, n - i)) {
      std::cout << i << " " << n - i << "\n";
      break;
    }
  }

  return 0;
}
