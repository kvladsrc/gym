#include <iostream>
#include <vector>

using std::vector;

// The partition algorithm is simplest for array with duplicates among
// those that I saw, much easier to make a mistake on l++&r--
// algorithm in case of duplicates.
static int partition(vector<int> &a, int l, int r) {
  int const pivot = a[r];

  // It always increase the sep before swapping.
  int sep = l - 1;
  for (int i = l; i <= (r - 1); ++i) {
    if (a[i] <= pivot) {
      std::swap(a[++sep], a[i]);
    }
  }

  // Important part!
  std::swap(a[++sep], a[r]);

  return sep;
}

static void quick_sort(vector<int> &a, int l, int r) {
  if (l >= r) {
    return;
  }

  int const pos = partition(a, l, r);
  quick_sort(a, l, pos - 1);
  quick_sort(a, pos + 1, r);
}

int main(int /*argc*/, char * /*argv*/[]) {
  int n = 0;
  std::cin >> n;
  vector<int> a(n);
  for (auto &i : a) {
    std::cin >> i;
  }

  quick_sort(a, 0, n - 1);

  for (auto i : a) {
    std::cout << i << " ";
  }
  std::cout << "\n";

  return 0;
}
